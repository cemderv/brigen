﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace brigen.Properties
{
  /// <summary>
  ///   A strongly-typed resource class, for looking up localized strings, etc.
  /// </summary>
  // This class was auto-generated by the StronglyTypedResourceBuilder
  // class via a tool like ResGen or Visual Studio.
  // To add or remove a member, edit your .ResX file then rerun ResGen
  // with the /str option, or rebuild your VS project.
  [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
  [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
  [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
  internal class Resources
  {

    private static global::System.Resources.ResourceManager resourceMan;

    private static global::System.Globalization.CultureInfo resourceCulture;

    [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal Resources()
    {
    }

    /// <summary>
    ///   Returns the cached ResourceManager instance used by this class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static global::System.Resources.ResourceManager ResourceManager
    {
      get
      {
        if (object.ReferenceEquals(resourceMan, null))
        {
          global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("lib.Properties.Resources", typeof(Resources).Assembly);
          resourceMan = temp;
        }
        return resourceMan;
      }
    }

    /// <summary>
    ///   Overrides the current thread's CurrentUICulture property for all
    ///   resource lookups using this strongly typed resource class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static global::System.Globalization.CultureInfo Culture
    {
      get
      {
        return resourceCulture;
      }
      set
      {
        resourceCulture = value;
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to template &lt;typename T&gt; class ArrayView
    ///{
    ///public:
    ///  ArrayView()
    ///      : m_Ptr(nullptr)
    ///      , m_Size(0u)
    ///  {
    ///  }
    ///
    ///  ArrayView(const T* ptr, size_t size)
    ///      : m_Ptr(ptr)
    ///      , m_Size(size)
    ///  {
    ///  }
    ///
    ///  template &lt;size_t N&gt;
    ///  ArrayView(const T (&amp;data)[N])
    ///      : m_Ptr(data)
    ///      , m_Size(N)
    ///  {
    ///  }
    ///
    ///${INIT_LIST_OVERLOAD}
    ///
    ///  [[nodiscard]]
    ///  bool empty() const
    ///  {
    ///    return m_Size &gt; 0u;
    ///  }
    ///
    ///  const T* begin() const
    ///  {
    ///    return m_Ptr;
    ///  }
    ///
    ///  const T* cbegin() const
    ///  {
    ///    return m_Ptr;
    ///  }
    ///
    ///  const T* e [rest of string was truncated]&quot;;.
    /// </summary>
    internal static string TS_ArrayView
    {
      get
      {
        return ResourceManager.GetString("TS_ArrayView", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to   ArrayView(std::initializer_list&lt;T&gt; initList)
    ///      : m_Ptr(initList.begin())
    ///      , m_Size(initList.size())
    ///  {
    ///  }.
    /// </summary>
    internal static string TS_ArrayViewInitListOverload
    {
      get
      {
        return ResourceManager.GetString("TS_ArrayViewInitListOverload", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to template &lt;typename T, typename U, size_t Size&gt; class ClassBuffer final
    ///{
    ///public:
    ///  explicit ClassBuffer(U* const* data, uint32_t size)
    ///      : m_Large(nullptr)
    ///      , m_Size(size)
    ///  {
    ///    T* ptr = nullptr;
    ///    if (size &gt; Size)
    ///    {
    ///      m_Large = static_cast&lt;T*&gt;(std::malloc(sizeof(T) * size));
    ///      ptr     = m_Large;
    ///    }
    ///    else
    ///    {
    ///      ptr = m_Small;
    ///    }
    ///
    ///    if (ptr != nullptr)
    ///    {
    ///      for (uint32_t i = 0u; i &lt; size; ++i)
    ///      {
    ///        *reinterpret_cast&lt;uintptr_t**&gt;(&amp;ptr[i]) =
    ///          [rest of string was truncated]&quot;;.
    /// </summary>
    internal static string TS_CClassBuffer
    {
      get
      {
        return ResourceManager.GetString("TS_CClassBuffer", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to #define ${MOD}_CLASS_(className)                       \
    ///public:                                                \
    ///  className();                                         \
    ///  explicit className(className##Impl* impl);           \
    ///  className(const className&amp; copyFrom);                \
    ///  className&amp; operator=(const className&amp; copyFrom);     \
    ///  className(className&amp;&amp; moveFrom) noexcept;            \
    ///  className&amp; operator=(className&amp;&amp; moveFrom) noexcept; \
    ///  ~className() noexcept;                               \ [rest of string was truncated]&quot;;.
    /// </summary>
    internal static string TS_ClassPrereqs
    {
      get
      {
        return ResourceManager.GetString("TS_ClassPrereqs", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to class Bool {
    ///public:
    ///  Bool()
    ///      : m_Value(0) {
    ///  }
    ///
    ///  Bool(bool value)
    ///      : m_Value(static_cast&lt;int32_t&gt;(value)) {
    ///  }
    ///
    ///  operator bool() const noexcept {
    ///    return static_cast&lt;bool&gt;(m_Value);
    ///  }
    ///
    ///private:
    ///  int32_t m_Value;
    ///};.
    /// </summary>
    internal static string TS_CppBool32
    {
      get
      {
        return ResourceManager.GetString("TS_CppBool32", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to #define ${MOD}_DEFINE_ENUM_FLAG_OPS(enumName)                    \
    ///  static inline enumName operator&amp;(enumName lhs, enumName rhs) { \
    ///    return enumName(int(lhs) &amp; int(rhs));                        \
    ///  }                                                              \
    ///  static inline enumName operator|(enumName lhs, enumName rhs) { \
    ///    return enumName(int(lhs) | int(rhs));                        \
    ///  }                                                              \
    ///  static inline bool HasFlag(enumName flags [rest of string was truncated]&quot;;.
    /// </summary>
    internal static string TS_CppDefineEnumOps
    {
      get
      {
        return ResourceManager.GetString("TS_CppDefineEnumOps", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to #define ${mod}_InvokeFunction(functor, ...) functor(functor##_sysValue, __VA_ARGS__).
    /// </summary>
    internal static string TS_CppInvokeFunction
    {
      get
      {
        return ResourceManager.GetString("TS_CppInvokeFunction", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to namespace brigen_internal
    ///{
    ///namespace detail
    ///{
    ///template &lt;typename T, typename = void&gt;
    ///struct is_noarg_callable_t : public std::false_type
    ///{
    ///};
    ///
    ///template &lt;typename T&gt;
    ///struct is_noarg_callable_t&lt;T, decltype(std::declval&lt;T&amp;&amp;&gt;()())&gt;
    ///    : public std::true_type
    ///{
    ///};
    ///
    ///template &lt;typename T&gt;
    ///struct returns_void_t
    ///    : public std::is_same&lt;void, decltype(std::declval&lt;T&amp;&amp;&gt;()())&gt;
    ///{
    ///};
    ///
    ///template &lt;typename T&gt;
    ///struct is_nothrow_invocable_if_required_t
    ///    : public
    ///#ifdef BRIGEN_SG_REQUIRE_NOEXCEPT
    ///      std::is_nothrow_invoc [rest of string was truncated]&quot;;.
    /// </summary>
    internal static string TS_CppScopeGuard
    {
      get
      {
        return ResourceManager.GetString("TS_CppScopeGuard", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to #ifdef ${MOD}_CLASS_
    ///#undef ${MOD}_CLASS_
    ///#endif
    ///
    ///#ifdef ${MOD}_DEFINE_ENUM_FLAG_OPS
    ///#undef ${MOD}_DEFINE_ENUM_FLAG_OPS
    ///#endif.
    /// </summary>
    internal static string TS_CppUndefs
    {
      get
      {
        return ResourceManager.GetString("TS_CppUndefs", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to template &lt;typename TContainer, typename T&gt;
    ///static inline void GetArrayHelper(const TContainer&amp; container, T* resultArray,
    ///                                  uint32_t* resultArraySize)
    ///{
    ///  if (!resultArray &amp;&amp; resultArraySize)
    ///  {
    ///    *resultArraySize = uint32_t(container.size());
    ///    return;
    ///  }
    ///
    ///  size_t numElementsToGet =
    ///      resultArraySize ? size_t(*resultArraySize) : container.size();
    ///
    ///  if (numElementsToGet &gt; container.size())
    ///    numElementsToGet = container.size();
    ///
    ///  if (resultArray)
    ///  {
    ///    for (s [rest of string was truncated]&quot;;.
    /// </summary>
    internal static string TS_GetArrayHelper
    {
      get
      {
        return ResourceManager.GetString("TS_GetArrayHelper", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to template &lt;typename TContainer, typename T&gt;
    ///static inline ArrayView&lt;T&gt; MakeArrayView(const TContainer&amp; container)
    ///{
    ///  return {container.data(), container.size()};
    ///}
    ///.
    /// </summary>
    internal static string TS_MakeArrayView
    {
      get
      {
        return ResourceManager.GetString("TS_MakeArrayView", resourceCulture);
      }
    }

    /// <summary>
    ///   Looks up a localized string similar to #define ADD_REF_IMPL() \
    ///  if (m_Impl != nullptr) { \
    ///    ObjectImplBase* obj = static_cast&lt;ObjectImplBase*&gt;(m_Impl); \
    ///    obj-&gt;AddRef(); \
    ///  }
    ///
    ///#define RELEASE_IMPL() \
    ///  if (m_Impl != nullptr) { \
    ///    ObjectImplBase* obj = static_cast&lt;ObjectImplBase*&gt;(m_Impl); \
    ///    obj-&gt;Release(); \
    ///    m_Impl = nullptr; \
    ///  }
    ///
    ///#define DEFINE_OBJ_CONSTRUCT(className) \
    ///  className::className() \
    ///    : m_Impl(nullptr) {} \
    ///  className::className(className##Impl* impl) \
    ///    : m_Impl(impl) { \
    ///    ADD_REF_IMPL(); \
    ///  } \
    /// [rest of string was truncated]&quot;;.
    /// </summary>
    internal static string TS_ObjectImplBase
    {
      get
      {
        return ResourceManager.GetString("TS_ObjectImplBase", resourceCulture);
      }
    }
  }
}
