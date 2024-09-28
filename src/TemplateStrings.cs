using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace brigen
{
    internal static class TemplateStrings
    {
        public const string ClassPrereqs = @"#define ${MOD}_CLASS_(className)                       \
public:                                                \
  className();                                         \
  explicit className(className##Impl* impl);           \
  className(const className& copyFrom);                \
  className& operator=(const className& copyFrom);     \
  className(className&& moveFrom) noexcept;            \
  className& operator=(className&& moveFrom) noexcept; \
  ~className() noexcept;                               \
  bool ${IS_VALID}() const;                            \
  explicit operator bool() const {                     \
    return this->${IS_VALID}();                        \
  }                                                    \
  bool operator==(const className& rhs) const {        \
    return m_Impl == rhs.m_Impl;                       \
  }                                                    \
  bool operator!=(const className& rhs) const {        \
    return m_Impl != rhs.m_Impl;                       \
  }                                                    \
  className##Impl* ${GET_IMPL}() const {               \
    return m_Impl;                                     \
  }                                                    \
  void ${SET_IMPL}(className##Impl* impl);             \
  className##Impl* ${DROP_IMPL}() {                    \
    className##Impl* ptr = m_Impl;                     \
    m_Impl = nullptr;                                  \
    return ptr;                                        \
  }                                                    \
private:                                               \
  className##Impl* m_Impl;                             \
public:";

        public const string CppBool32 = @"class bool32_t {
public:
  bool32_t()
      : m_Value(0) {
  }

  bool32_t(bool value) // NOLINT(google-explicit-constructor)
      : m_Value(static_cast<int32_t>(value)) {
  }

  operator bool() const noexcept { // NOLINT(google-explicit-constructor)
    return static_cast<bool>(m_Value);
  }

private:
  int32_t m_Value;
};";

        public const string CppDefineEnumOps = @"#define ${MOD}_DEFINE_ENUM_FLAG_OPS(enumName)                    \
  static inline enumName operator&(enumName lhs, enumName rhs) { \
    return enumName(int(lhs) & int(rhs));                        \
  }                                                              \
  static inline enumName operator|(enumName lhs, enumName rhs) { \
    return enumName(int(lhs) | int(rhs));                        \
  }                                                              \
  static inline bool HasFlag(enumName flags, enumName test)    { \
    return (flags & test) == test;                               \
  }";

        public const string CppScopeGuard = @"namespace brigen_internal
{
namespace detail
{
template <typename T, typename = void>
struct is_noarg_callable_t : public std::false_type
{
};

template <typename T>
struct is_noarg_callable_t<T, decltype(std::declval<T&&>()())>
    : public std::true_type
{
};

template <typename T>
struct returns_void_t
    : public std::is_same<void, decltype(std::declval<T&&>()())>
{
};

template <typename T>
struct is_nothrow_invocable_if_required_t
    : public
#ifdef BRIGEN_SG_REQUIRE_NOEXCEPT
      std::is_nothrow_invocable<T> /* Note: _r variants not enough to
                                   confirm void return: any return can be
                                   discarded so all returns are
                                   compatible with void */
#else
      std::true_type
#endif
{
};

template <typename A, typename B, typename... C>
struct and_t : public and_t<A, and_t<B, C...>>
{
};

template <typename A, typename B>
struct and_t<A, B> : public std::conditional<A::value, B, A>::type
{
};

template <typename T>
struct is_proper_sg_callback_t
    : public and_t<is_noarg_callable_t<T>, returns_void_t<T>,
                   is_nothrow_invocable_if_required_t<T>,
                   std::is_nothrow_destructible<T>>
{
};

template <typename Callback,
          typename = typename std::enable_if<
              is_proper_sg_callback_t<Callback>::value>::type>
class scope_guard;

template <typename Callback>
detail::scope_guard<Callback> make_scope_guard(Callback&& callback) noexcept(
    std::is_nothrow_constructible<Callback, Callback&&>::value);

template <typename Callback> class scope_guard<Callback> final
{
public:
  typedef Callback callback_type;

  scope_guard(scope_guard&& other) noexcept(
      std::is_nothrow_constructible<Callback, Callback&&>::value);

  ~scope_guard() noexcept;

  void dismiss() noexcept;

public:
  scope_guard()                   = delete;
  scope_guard(const scope_guard&) = delete;
  scope_guard& operator=(const scope_guard&) = delete;
  scope_guard& operator=(scope_guard&&) = delete;

private:
  explicit scope_guard(Callback&& callback) noexcept(
      std::is_nothrow_constructible<Callback, Callback&&>::value);

  friend scope_guard<Callback> make_scope_guard<Callback>(Callback&&) noexcept(
      std::is_nothrow_constructible<Callback, Callback&&>::value);

private:
  Callback m_callback;
  bool     m_active;
};
} // namespace detail

using detail::make_scope_guard;

template <typename Callback>
detail::scope_guard<Callback>::scope_guard(Callback&& callback) noexcept(
    std::is_nothrow_constructible<Callback, Callback&&>::value)
    : m_callback(std::forward<Callback>(callback))
    , m_active{true}
{
}

template <typename Callback>
detail::scope_guard<Callback>::~scope_guard() noexcept
{
  if (m_active)
    m_callback();
}

template <typename Callback>
detail::scope_guard<Callback>::scope_guard(scope_guard&& other) noexcept(
    std::is_nothrow_constructible<Callback, Callback&&>::value)
    : m_callback(std::forward<Callback>(other.m_callback)) // idem
    , m_active{std::move(other.m_active)}
{
  other.m_active = false;
}

template <typename Callback>
inline void detail::scope_guard<Callback>::dismiss() noexcept
{
  m_active = false;
}

template <typename Callback>
inline auto detail::make_scope_guard(Callback&& callback) noexcept(
    std::is_nothrow_constructible<Callback, Callback&&>::value)
    -> detail::scope_guard<Callback>
{
  return detail::scope_guard<Callback>{std::forward<Callback>(callback)};
}
} // namespace brigen_internal";

        public const string CppUndefs = @"#ifdef ${MOD}_CLASS_
#undef ${MOD}_CLASS_
#endif

#ifdef ${MOD}_DEFINE_ENUM_FLAG_OPS
#undef ${MOD}_DEFINE_ENUM_FLAG_OPS
#endif";

        public const string GetArrayHelper = @"template <typename TContainer, typename T>
static inline void GetArrayHelper(const TContainer& container, T* resultArray,
                                  uint32_t* resultArraySize)
{
  if (!resultArray && resultArraySize)
  {
    *resultArraySize = uint32_t(container.size());
    return;
  }

  size_t numElementsToGet =
      resultArraySize ? size_t(*resultArraySize) : container.size();

  if (numElementsToGet > container.size())
    numElementsToGet = container.size();

  if (resultArray)
  {
    for (size_t i = 0u; i < numElementsToGet; ++i)
      resultArray[i] = static_cast<T>(container[i]);
  }
}";

        public const string ObjectImplBase = @"#define ADD_REF_IMPL() \
  if (m_Impl != nullptr) { \
    ObjectImplBase* obj = static_cast<ObjectImplBase*>(m_Impl); \
    obj->AddRef(); \
  }

#define RELEASE_IMPL() \
  if (m_Impl != nullptr) { \
    ObjectImplBase* obj = static_cast<ObjectImplBase*>(m_Impl); \
    obj->Release(); \
    m_Impl = nullptr; \
  }

#define DEFINE_OBJ_CONSTRUCT(className) \
  className::className() \
    : m_Impl(nullptr) {} \
  className::className(className##Impl* impl) \
    : m_Impl(impl) { \
    ADD_REF_IMPL(); \
  } \
  className::className(const className& copyFrom) \
      : m_Impl(copyFrom.m_Impl) { \
    ADD_REF_IMPL(); \
  } \
  className& className::operator=(const className& copyFrom) { \
    if (&copyFrom != this) { \
      RELEASE_IMPL(); \
      m_Impl = copyFrom.m_Impl; \
      ADD_REF_IMPL(); \
    } \
    return *this; \
  } \
  className::className(className&& moveFrom) noexcept \
      : m_Impl(moveFrom.m_Impl) { \
    moveFrom.m_Impl = nullptr; \
  } \
  className& className::operator=(className&& moveFrom) noexcept { \
    if (&moveFrom != this) { \
      RELEASE_IMPL(); \
      m_Impl          = moveFrom.m_Impl; \
      moveFrom.m_Impl = nullptr; \
    } \
    return *this; \
  } \
  className::~className() noexcept { \
    RELEASE_IMPL(); \
  } \
  bool className::${IS_VALID}() const { \
    return m_Impl != nullptr; \
  } \
  void className::${SET_IMPL}(className##Impl* impl) { \
    RELEASE_IMPL(); \
    m_Impl = impl; \
    ADD_REF_IMPL(); \
  }

class ObjectImplBase
{
public:
  ObjectImplBase()
    : m_RefCount(0u)
  { }

  ObjectImplBase(const ObjectImplBase&) = delete;

  void operator=(const ObjectImplBase&) = delete;

  ObjectImplBase(ObjectImplBase&&) noexcept = delete;

  void operator=(ObjectImplBase&&) noexcept = delete;

  virtual ~ObjectImplBase() noexcept = default;

  uint32_t AddRef() {
    return ++m_RefCount;
  }

  uint32_t Release() {
    const uint32_t refCount = --m_RefCount;

    assert(refCount != static_cast<uint32_t>(-1));

    if (refCount == 0u) {
      delete this;
    }

    return refCount;
  }

private:
  std::atomic<uint32_t> m_RefCount;
};";

        public const string CppInvokeFunction = @"#define ${mod}_InvokeFunction(functor, ...) functor(functor##_sysValue, __VA_ARGS__)";

        public const string CClassBuffer = @"template <typename T, typename U, size_t Size>
class ClassBuffer final {
public:
  explicit ClassBuffer(U* const* data, uint32_t size)
      : m_Large(nullptr)
      , m_Size(size)
  {
    T* ptr = nullptr;
    if (size > Size) {
      m_Large = static_cast<T*>(std::malloc(sizeof(T) * size));
      ptr     = m_Large;
    }
    else {
      ptr = m_Small;
    }

    if (ptr != nullptr) {
      for (uint32_t i = 0u; i < size; ++i) {
        *reinterpret_cast<uintptr_t**>(&ptr[i]) =
            const_cast<uintptr_t*>(reinterpret_cast<const uintptr_t*>(data[i]));
      }
    }
  }

  ClassBuffer(const ClassBuffer&) = delete;

  void operator=(const ClassBuffer&) = delete;

  ClassBuffer(ClassBuffer&&) noexcept = delete;

  void operator=(ClassBuffer&&) noexcept = delete;

  ~ClassBuffer() noexcept {
    if (m_Size > 0u) {
      T* const ptr = m_Large != nullptr ? m_Large : m_Small;

      for (uint32_t i = 0u; i < m_Size; ++i) {
        *reinterpret_cast<uintptr_t**>(&ptr[i]) = nullptr;
      }
    }

    if (m_Large != nullptr) {
      std::free(m_Large);
      m_Large = nullptr;
    }
  }

  const T* GetData() const {
    return m_Large != nullptr ? m_Large : m_Small;
  }

private:
  T        m_Small[Size];
  T*       m_Large;
  uint32_t m_Size;
};";

    }
}
