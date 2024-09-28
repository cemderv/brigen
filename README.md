# brigen

Write an interface, implement in C++, access from various languages.

brigen is able to transform an interface definition file to its idiomatic equivalents in languages such as
C, C++ and C# using a stable, common denominator C API.

The generated API can be internally implemented in a modern C++ standard.


<img src="https://github.com/cemderv/brigen/blob/main/misc/brigen_diag.png?raw=true" width="600">

## Example

```cpp
// MyLib.interface
module MyLib;

// set variables such as naming style, library description, ...
set companyid somecompany

enum SomeEnum {
  Member1 = 1,
  Member2 = 2,
}

// Structs are pure value types (stack-allocated).
struct Vector2 {
  float X;
  float Y;
}

struct SomeStruct {
  int X;
  SomeEnum E;
  Vector2 V;
}

delegate int SomeCallbackFunction(int x);

// Classes are reference types (heap-allocated, reference-counted).
class SomeClass {
  ctor Create(int x);
  func int Add(Vector2 a, int b);
  func void CallMe(SomeCallbackFunction callback);
  get int SomeReadOnlyProperty;
  set int SomeWriteOnlyProperty;
  get set int SomeProperty;
}
```

---

Would generate an interface that is usable as follows:

#### C
```c
MyLib_SomeClass* c = MyLib_SomeClass_Create(10); // Reference count = 1

MyLib_SomeStruct s = (MyLib_SomeStruct){
  .X = 20,
  .E = MyLib_SomeEnum_Member1,
  .V = (MyLib_Vector2){ .X = 1.0f, .Y = 2.0f },
};

MyLib_SomeClass_SetSomeProperty(c, MyLib_SomeClass_Add(&s, 20));

MyLib_SomeClass_Release(c); // Release c, destroying it
```

#### C++
```cpp
using namespace MyLib;

auto c = SomeClass::Create(10); // Reference-counted, well-behaved RAII object

auto s = SomeStruct{
  .X = 20,
  .E = SomeEnum::Member1,
  .V = { 1.0f, 2.0f },
};

c.SetSomeProperty(c.Add(s, 20))
```

#### C#
```csharp
using MyLib;

using var c = SomeClass.Create(10); // Classes implement IDisposable

var s = new SomeStruct(20, SomeEnum.Member1, new Vector2(1, 2));

c.SomeProperty = c.Add(s, 20);
```

## Building and Usage

brigen is written in C# and requires .NET 8 or newer.
The repository contains a solution that can be opened directly using Visual Studio.

Alternatively:

```bash
dotnet publish -o bin
```

Will produce a standalone (ahead-of-time compiled) executable in `bin/`.
Example:

```bash
./bin/brigen --in=MyLib.interface --csharp --python --java
```

For help, see command `brigen --help`.

## FAQ

### Is this another RPC library?

No, brigen does not generate a library that supports remote procedure calls (RPC).

The generated library is intended to be called from within the same process / address space.

It generates a foreign function interface (ffi). This is comparable to technologies such as [MIDL](https://learn.microsoft.com/en-us/windows/win32/midl/midl-start-page).

### Difference between this and SWIG?

brigen and SWIG have different goals.

While [SWIG](https://www.swig.org/) allows you to generate bindings for an existing interface or library,
brigen generates bindings **together with a new library**.

brigen is therefore useful for when you are building a library that requires certain, strict semantics such
as separation between value and reference types (reference counted).

By doing so, brigen generates APIs that integrate with other languages in an idiomatic way, since it does not
have to guess the semantics of an existing library.


