# Coding Standards

This document defines coding standards for the project. All code should follow these guidelines to ensure consistency, maintainability, and quality.

## General Principles

- **Readability over cleverness**: Code is read more often than written
- **SOLID principles**: Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion
- **DRY (Don't Repeat Yourself)**: Avoid code duplication
- **YAGNI (You Aren't Gonna Need It)**: Don't add functionality until needed
- **Fail fast**: Validate inputs and fail early with clear error messages
- **Meaningful names**: Use descriptive names for variables, functions, and classes

## C# Coding Standards

### C# Naming Conventions

- **PascalCase** for class names, method names, properties, public fields
- **camelCase** for local variables, method parameters, private fields
- **_camelCase** (underscore prefix) for private instance fields
- **UPPER_CASE** for constants
- Use descriptive names that convey intent

```csharp
public class UserAuthenticationService  // PascalCase for classes
{
    private readonly IUserRepository _userRepository;  // _camelCase for private fields
    private const int MAX_LOGIN_ATTEMPTS = 5;  // UPPER_CASE for constants
    
    public async Task<AuthResult> AuthenticateAsync(string username, string password)  // PascalCase for methods
    {
        var user = await _userRepository.FindByUsernameAsync(username);  // camelCase for local vars
        // ...
    }
}
```

### Code Structure

- **One class per file**: File name should match class name
- **Organize using statements**: Remove unused, group by System, then third-party, then project
- **Order class members**: Constants, fields, constructors, properties, methods
- **Maximum method length**: Keep methods under 50 lines when possible
- **Maximum class length**: Keep classes under 500 lines; refactor if larger

### C# Dependency Injection

```csharp
// Good: Constructor injection with null checks
public class OAuthAuthenticationService : IOAuthAuthenticationService
{
    private readonly IOAuthProviderFactory _providerFactory;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<OAuthAuthenticationService> _logger;

    public OAuthAuthenticationService(
        IOAuthProviderFactory providerFactory,
        IUserRepository userRepository,
        ILogger<OAuthAuthenticationService> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}

// Bad: Service locator or property injection (avoid)
```

### Async/Await

- **Always use async/await for I/O operations**: Database, network, file system
- **Never block on async code**: Don't use `.Result` or `.Wait()`
- **Suffix async methods with "Async"**: `GetUserAsync()`, not `GetUser()`
- **ConfigureAwait(false)** in library code to avoid context capture

```csharp
// Good
public async Task<User> GetUserAsync(int userId)
{
    return await _userRepository.FindByIdAsync(userId);
}

// Bad: Blocking on async code
public User GetUser(int userId)
{
    return _userRepository.FindByIdAsync(userId).Result;  // DON'T DO THIS
}
```

### C# Error Handling

```csharp
// Good: Specific exceptions, meaningful messages, logging
public async Task<AuthResult> HandleCallbackAsync(string code, string state)
{
    try
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Authorization code is required", nameof(code));
        
        if (!await ValidateStateAsync(state))
        {
            _logger.LogWarning("Invalid state parameter in OAuth callback");
            return AuthResult.Failure("Invalid authentication request");
        }
        
        // ... rest of implementation
    }
    catch (OAuthException ex)
    {
        _logger.LogError(ex, "OAuth authentication failed");
        return AuthResult.Failure("Authentication failed. Please try again.");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during OAuth callback");
        throw;  // Re-throw unexpected exceptions
    }
}

// Bad: Catching all exceptions without logging, generic messages
```

### LINQ Usage

```csharp
// Good: Clear, readable LINQ
var activeUsers = users
    .Where(u => u.IsActive)
    .OrderBy(u => u.LastName)
    .ThenBy(u => u.FirstName)
    .ToList();

// Acceptable for simple cases
var activeUsers = users.Where(u => u.IsActive).ToList();

// Bad: Overly complex LINQ that should be broken down
var result = users.Where(u => u.IsActive)
    .GroupBy(u => u.Department)
    .Select(g => new { Dept = g.Key, Count = g.Count(), Avg = g.Average(u => u.Salary) })
    .OrderByDescending(x => x.Avg)
    .Take(10)
    .ToDictionary(x => x.Dept, x => x.Avg);  // Consider breaking this down
```

### Null Handling

```csharp
// Good: Null checks and nullable reference types (C# 8+)
public async Task<User?> FindUserAsync(string username)
{
    if (string.IsNullOrWhiteSpace(username))
        return null;
    
    return await _userRepository.FindByUsernameAsync(username);
}

// Good: Null-coalescing and null-conditional operators
var displayName = user?.FullName ?? "Unknown User";
var emailCount = user?.Emails?.Count ?? 0;

// Good: Pattern matching for null checks (C# 9+)
if (user is not null)
{
    ProcessUser(user);
}
```

### C# Comments and Documentation

```csharp
// Good: XML documentation for public APIs
/// <summary>
/// Authenticates a user using OAuth provider.
/// </summary>
/// <param name="providerName">The OAuth provider name (e.g., "google", "github").</param>
/// <param name="redirectUri">The URI to redirect after authentication.</param>
/// <returns>An <see cref="AuthorizationUrl"/> containing the URL to redirect the user to.</returns>
/// <exception cref="ArgumentException">Thrown when providerName is null or empty.</exception>
public async Task<AuthorizationUrl> InitiateAuthAsync(string providerName, string redirectUri)
{
    // Implementation
}

// Good: Comments explaining "why", not "what"
// Retry token exchange up to 3 times to handle transient network errors
for (int attempt = 0; attempt < 3; attempt++)
{
    // ...
}

// Bad: Comments explaining obvious code
// Loop through users
foreach (var user in users)  // This comment adds no value
{
    // ...
}
```

## C++ Coding Standards

### C++ Naming Conventions

- **PascalCase** for class names, struct names
- **camelCase** for function names, variable names, member variables
- **UPPER_CASE** for macros and constants
- **kPascalCase** for constants (alternative style)

```cpp
class OAuthProvider {  // PascalCase for classes
public:
    std::string getAuthorizationUrl(const std::string& state) const;  // camelCase for methods
    
private:
    std::string clientId_;  // camelCase with trailing underscore for members
    const int MAX_RETRIES = 3;  // UPPER_CASE for constants
};
```

### Modern C++ Features (C++17/20)

```cpp
// Good: Use auto for type deduction when type is obvious
auto user = userRepository.findById(userId);
auto users = std::vector<User>{};

// Good: Use range-based for loops
for (const auto& user : users) {
    processUser(user);
}

// Good: Use structured bindings (C++17)
auto [success, token] = exchangeCodeForToken(code);

// Good: Use std::optional for optional values (C++17)
std::optional<User> findUser(const std::string& username) {
    // Return std::nullopt if not found
}

// Good: Use std::string_view for string parameters (C++17)
void processUsername(std::string_view username) {
    // ...
}
```

### Memory Management (RAII)

```cpp
// Good: Use smart pointers, avoid raw new/delete
auto httpClient = std::make_unique<HttpClient>();
auto sharedCache = std::make_shared<TokenCache>();

// Good: RAII for resource management
class FileHandle {
public:
    explicit FileHandle(const std::string& filename)
        : file_(std::fopen(filename.c_str(), "r")) {
        if (!file_) {
            throw std::runtime_error("Failed to open file");
        }
    }
    
    ~FileHandle() {
        if (file_) {
            std::fclose(file_);
        }
    }
    
    // Delete copy, allow move
    FileHandle(const FileHandle&) = delete;
    FileHandle& operator=(const FileHandle&) = delete;
    FileHandle(FileHandle&&) = default;
    FileHandle& operator=(FileHandle&&) = default;
    
private:
    FILE* file_;
};

// Bad: Manual memory management
HttpClient* client = new HttpClient();  // Memory leak risk
// ... use client
delete client;  // Easy to forget or miss on error paths
```

### Const Correctness

```cpp
// Good: Use const for parameters that won't be modified
std::string getAuthorizationUrl(const std::string& state, 
                                 const std::string& redirectUri) const;

// Good: Use const member functions when they don't modify state
class OAuthProvider {
public:
    std::string getClientId() const { return clientId_; }
    bool isConfigured() const { return !clientId_.empty(); }
    
private:
    std::string clientId_;
};
```

### C++ Error Handling

```cpp
// Good: Use exceptions for exceptional circumstances
class OAuthException : public std::runtime_error {
public:
    explicit OAuthException(const std::string& message)
        : std::runtime_error(message) {}
};

TokenResponse exchangeCodeForToken(const std::string& code) {
    if (code.empty()) {
        throw std::invalid_argument("Code cannot be empty");
    }
    
    // ... implementation
    
    if (response.status != 200) {
        throw OAuthException("Token exchange failed");
    }
    
    return tokenResponse;
}

// Alternative: Use std::expected (C++23) or result types for expected errors
std::optional<TokenResponse> tryExchangeCodeForToken(const std::string& code) {
    // Return std::nullopt on expected failures
}
```

### Header Files

```cpp
// Good: Header guards using #pragma once (widely supported)
#pragma once

#include <string>
#include <memory>

class OAuthProvider {
    // ... class definition
};

// Alternative: Traditional include guards
#ifndef OAUTH_PROVIDER_H
#define OAUTH_PROVIDER_H
// ... content
#endif
```

### C++ Comments and Documentation

```cpp
/// @brief Authenticates a user using the specified OAuth provider.
/// @param providerName The name of the OAuth provider (e.g., "google").
/// @param redirectUri The URI to redirect to after authentication.
/// @return An AuthorizationUrl containing the redirect URL.
/// @throws std::invalid_argument if providerName is empty.
AuthorizationUrl initiateAuth(const std::string& providerName,
                               const std::string& redirectUri);

// Explain complex algorithms or non-obvious decisions
// Using exponential backoff for retries to avoid overwhelming the OAuth server
// Backoff formula: delay = baseDelay * 2^attempt
for (int attempt = 0; attempt < maxRetries; ++attempt) {
    // ...
}
```

## C / Embedded C Coding Standards

### C Naming Conventions

- **snake_case** for function names, variable names, and file names
- **PascalCase** or **UPPER_CASE** for type names (`typedef struct`)
- **UPPER_CASE** for macros, constants, and enum values
- **module_** prefix for public functions to simulate namespaces
- **k** prefix for file-scoped constants (optional alternative: `UPPER_CASE`)

```c
/* Naming examples */
#define MAX_BUFFER_SIZE 1024          /* UPPER_CASE for macros/constants */
#define UART_BAUD_RATE  115200

typedef struct SensorReading {        /* PascalCase for types */
    float temperature;                /* snake_case for members */
    float humidity;
    uint32_t timestamp_ms;
} SensorReading;

static int retry_count = 0;          /* snake_case for variables */

/* module_ prefix for public functions */
int sensor_init(const SensorConfig *config);
int sensor_read(SensorReading *out_reading);
void sensor_shutdown(void);
```

### C Code Structure

- **One module per file pair**: `module.h` (public API) + `module.c` (implementation)
- **Header guards** in every header file
- **Include order**: standard library, third-party, project headers (alphabetical within each group)
- **Minimize header includes**: forward-declare where possible; include only what you need
- **Static for file-scoped symbols**: Mark all internal functions and variables `static`
- **Maximum function length**: Keep functions under 50 lines when possible

```c
/* sensor.h */
#ifndef SENSOR_H
#define SENSOR_H

#include <stdint.h>    /* Standard library first */
#include <stdbool.h>

/* Forward-declare types used only as pointers */
typedef struct SensorConfig SensorConfig;

/* Public API — prefixed with module name */
int  sensor_init(const SensorConfig *config);
int  sensor_read(SensorReading *out_reading);
bool sensor_is_ready(void);
void sensor_shutdown(void);

#endif /* SENSOR_H */
```

### Memory Management

- **No memory leaks**: Every `malloc` must have a corresponding `free`
- **Check return values**: Always check `malloc` (and similar) for `NULL`
- **Single-owner model**: One module "owns" allocated memory; others borrow via pointers
- **Cleanup on all paths**: Use `goto cleanup` pattern for multi-resource functions
- **Minimize dynamic allocation**: Prefer stack or static allocation in embedded contexts
- **Bounded buffers**: Always know and enforce buffer sizes

```c
/* Good: goto cleanup pattern for multi-resource functions */
int process_data(const char *filename)
{
    int result = -1;
    FILE *file = NULL;
    uint8_t *buffer = NULL;

    file = fopen(filename, "rb");
    if (!file) {
        log_error("Failed to open file: %s", filename);
        goto cleanup;
    }

    buffer = malloc(MAX_BUFFER_SIZE);
    if (!buffer) {
        log_error("Memory allocation failed");
        goto cleanup;
    }

    /* ... process data ... */
    result = 0;

cleanup:
    free(buffer);       /* free(NULL) is safe */
    if (file) {
        fclose(file);
    }
    return result;
}
```

### Safe String & Buffer Operations

- **Always bounds-check**: Use `snprintf`, never `sprintf`; use `strncpy` with explicit null termination
- **Prevent overflow**: Validate sizes before copying or indexing
- **No implicit trust**: Validate all external inputs (sensor data, network packets, user input)

```c
/* Good: Safe string formatting */
char msg[128];
int written = snprintf(msg, sizeof(msg), "Sensor %d: %.2f°C", id, temp);
if (written < 0 || (size_t)written >= sizeof(msg)) {
    log_warning("Message truncated");
}

/* Good: Bounded copy with null termination */
char dest[32];
strncpy(dest, src, sizeof(dest) - 1);
dest[sizeof(dest) - 1] = '\0';

/* Bad: Unbounded operations — never do this */
/* sprintf(msg, "Sensor %d: %f", id, temp);  */
/* strcpy(dest, src);                         */
```

### Embedded-Specific Guidelines

- **Interrupt safety**: Keep ISRs short; defer work to main loop or task
- **Volatile for hardware**: Use `volatile` for memory-mapped I/O and shared variables modified in ISRs
- **Atomic operations**: Use atomic types or critical sections for shared state
- **No heap in safety-critical code**: Use static allocation for deterministic memory
- **Real-time awareness**: Avoid unbounded loops and blocking calls in time-critical paths
- **Power management**: Design for low-power modes, minimize CPU wake time

```c
/* Good: Short ISR, deferred processing */
volatile bool g_data_ready = false;

void UART_IRQHandler(void)
{
    g_rx_buffer[g_rx_index++] = UART->DR;
    if (g_rx_index >= EXPECTED_PACKET_SIZE) {
        g_data_ready = true;  /* Signal main loop */
    }
    UART->ICR = UART_ICR_RXIC;  /* Clear interrupt flag */
}

/* Main loop processes data outside ISR context */
void main_loop(void)
{
    if (g_data_ready) {
        g_data_ready = false;
        process_packet(g_rx_buffer, g_rx_index);
        g_rx_index = 0;
    }
}
```

### C Error Handling

- **Return error codes**: Use negative values or named error codes (not just 0/1)
- **Check every return value**: From system calls, library functions, and hardware operations
- **Log errors with context**: Include function name, parameters, and error codes
- **No silent failures**: Every error path must log or propagate the error

```c
/* Good: Named error codes */
typedef enum {
    SENSOR_OK            =  0,
    SENSOR_ERR_TIMEOUT   = -1,
    SENSOR_ERR_CRC       = -2,
    SENSOR_ERR_OVERFLOW  = -3,
    SENSOR_ERR_NOT_INIT  = -4
} SensorError;

/* Good: Check and propagate all errors */
SensorError sensor_read(SensorReading *out)
{
    if (!out) {
        return SENSOR_ERR_NOT_INIT;
    }

    SensorError err = sensor_hw_trigger();
    if (err != SENSOR_OK) {
        log_error("sensor_read: trigger failed (err=%d)", err);
        return err;
    }

    err = sensor_hw_wait(TIMEOUT_MS);
    if (err != SENSOR_OK) {
        log_error("sensor_read: timeout waiting for data");
        return err;
    }

    return sensor_hw_fetch(out);
}
```

### C Comments and Documentation

```c
/**
 * @brief  Initialize the sensor module.
 * @param  config  Pointer to sensor configuration. Must not be NULL.
 * @retval SENSOR_OK on success, negative error code on failure.
 *
 * Configures hardware registers and starts the calibration sequence.
 * The sensor is ready for reads after this call returns SENSOR_OK.
 */
SensorError sensor_init(const SensorConfig *config);

/* Explain "why" — not "what" */
/* Use 16-bit CRC because the sensor protocol requires CRC-CCITT */
uint16_t crc = crc16_ccitt(buffer, length);
```

## JavaScript / TypeScript and Frontend Coding Standards

### JS/TS Naming Conventions

- **camelCase** for variables, functions, and methods
- **PascalCase** for classes, interfaces, types, enums, and React/Vue components
- **UPPER_CASE** for constants and environment variables
- **kebab-case** for file names (some frameworks prefer PascalCase for component files)
- **I** prefix for interfaces (optional — TypeScript community is split; be consistent)

```typescript
// Naming examples
const MAX_RETRIES = 3;                            // UPPER_CASE for constants
const apiBaseUrl = process.env.API_BASE_URL;      // camelCase for variables

interface UserProfile {                            // PascalCase for interfaces
  firstName: string;
  lastName: string;
  emailAddress: string;
}

class AuthenticationService {                      // PascalCase for classes
  private readonly httpClient: HttpClient;

  async authenticateUser(credentials: LoginCredentials): Promise<AuthResult> {
    // camelCase for methods and parameters
  }
}

function formatCurrency(amount: number, locale: string): string {
  // camelCase for functions
}
```

### TypeScript Best Practices

- **Always use TypeScript** over plain JavaScript for type safety
- **Strict mode**: Enable `strict: true` in `tsconfig.json`
- **Avoid `any`**: Use `unknown` for truly unknown types; create proper types otherwise
- **Use type inference**: Don't annotate when TypeScript can infer correctly
- **Prefer interfaces** for object shapes; use `type` for unions, intersections, and mapped types
- **Use enums sparingly**: Prefer `const` objects or union types for string literals

```typescript
// Good: Proper typing, no `any`
function processResponse(data: unknown): UserProfile {
  if (!isUserProfile(data)) {
    throw new Error('Invalid response shape');
  }
  return data;
}

// Good: Union types instead of enums for string literals
type Status = 'active' | 'inactive' | 'pending';

// Good: Generics for reusable types
interface ApiResponse<T> {
  data: T;
  status: number;
  message: string;
}

// Bad: Using `any` — defeats the purpose of TypeScript
function processResponse(data: any): any { /* ... */ }
```

### React Component Patterns

```tsx
// Good: Functional components with proper typing
interface UserCardProps {
  user: UserProfile;
  onSelect: (userId: string) => void;
  isHighlighted?: boolean;  // Optional props explicitly typed
}

const UserCard: React.FC<UserCardProps> = ({ user, onSelect, isHighlighted = false }) => {
  const handleClick = useCallback(() => {
    onSelect(user.id);
  }, [onSelect, user.id]);

  return (
    <div
      className={cn('user-card', { 'user-card--highlighted': isHighlighted })}
      onClick={handleClick}
      role="button"
      tabIndex={0}
      aria-label={`Select ${user.firstName} ${user.lastName}`}
    >
      <h3>{user.firstName} {user.lastName}</h3>
      <p>{user.emailAddress}</p>
    </div>
  );
};

// Good: Custom hooks for shared logic
function useDebounce<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}
```

### State Management

- **Component state** for local UI state (form inputs, toggles, modals)
- **Context** for shared state accessed by a subtree (theme, locale, auth)
- **External store** (Redux, Zustand, Pinia) for complex global state
- **Server state** (React Query, SWR, TanStack Query) for API data — don't duplicate server data in local state
- **Minimize state**: Derive values from existing state instead of creating new state

```typescript
// Good: Derived state instead of storing separately
const completedCount = todos.filter(t => t.completed).length;

// Bad: Storing derived data as separate state
const [completedCount, setCompletedCount] = useState(0);
// Now you have to keep completedCount in sync — error-prone
```

### Accessibility (WCAG 2.1 AA)

- **Semantic HTML**: Use proper elements (`<button>`, `<nav>`, `<main>`, `<h1>`-`<h6>`)
- **ARIA labels**: Add `aria-label`, `aria-describedby`, `role` where needed
- **Keyboard navigation**: All interactive elements reachable via Tab and operable via Enter/Space
- **Color contrast**: Minimum 4.5:1 for normal text, 3:1 for large text
- **Focus management**: Visible focus indicators; manage focus on route changes and modals
- **Screen reader testing**: Verify with NVDA, VoiceOver, or JAWS

### Performance

- **Code splitting**: Use dynamic `import()` for route-level and large component chunks
- **Lazy loading**: `React.lazy()` / `defineAsyncComponent()` for non-critical components
- **Memoization**: `React.memo`, `useMemo`, `useCallback` — but only when profiling shows a need
- **Image optimization**: Use `next/image`, `srcset`, or lazy loading for images
- **Bundle analysis**: Monitor bundle size with `webpack-bundle-analyzer` or equivalent
- **Avoid layout thrashing**: Batch DOM reads and writes; use `requestAnimationFrame`

### Frontend Security

- **XSS prevention**: Never use `dangerouslySetInnerHTML` / `v-html` with untrusted content
- **CSRF protection**: Include CSRF tokens in state-changing requests
- **Secure cookies**: `HttpOnly`, `Secure`, `SameSite` attributes
- **Content Security Policy**: Configure CSP headers to restrict script sources
- **Input sanitization**: Sanitize on the server; escape on the client
- **No secrets in client code**: API keys, tokens, and secrets belong on the server

### CSS / Styling

- **Consistent approach**: Pick one methodology (CSS Modules, Tailwind, styled-components, BEM) and use it project-wide
- **Responsive design**: Mobile-first; use min-width media queries
- **Design tokens**: Centralize colors, spacing, typography, and breakpoints in variables or a theme
- **No magic numbers**: Use named tokens for spacing, font sizes, and breakpoints

```css
/* Good: Using design tokens */
.card {
  padding: var(--spacing-md);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
}

/* Bad: Magic numbers */
.card {
  padding: 16px;
  border-radius: 8px;
  background: #f5f5f5;
  box-shadow: 0 1px 3px rgba(0,0,0,0.12);
}
```

## Other Languages

When using languages other than C#, C++, C, or JavaScript/TypeScript, follow the established conventions for that language:

### Python

- Follow PEP 8 style guide
- Use `snake_case` for functions and variables
- Use `PascalCase` for classes
- Type hints for function signatures (Python 3.5+)
- Docstrings for modules, classes, and functions (Google or NumPy style)
- Use virtual environments; pin dependency versions
- Prefer `pathlib` over `os.path` for file paths

### Rust

- Follow Rust style guide (rustfmt)
- Use `snake_case` for functions and variables
- Use `PascalCase` for types and traits
- Embrace ownership and borrowing
- Use `Result` and `Option` types appropriately
- Avoid `unwrap()` in production code — use `?` operator
- Clippy for linting

### Go

- Follow Go conventions (gofmt)
- Use `camelCase` for unexported, `PascalCase` for exported
- Keep packages small and focused
- Use interfaces for abstraction
- Handle errors explicitly — no panic in library code
- Use `context.Context` for cancellation and deadlines

## Testing Standards

### Unit Tests

#### C# Tests (xUnit/NUnit)

- Use constructor injection for test setup
- Mock dependencies using Moq or NSubstitute
- Use descriptive test names: `GivenCondition_WhenAction_ThenExpectedResult`
- Arrange-Act-Assert pattern
- One assertion per test when possible

#### C++ Tests (Google Test/Catch2)

- Use TEST or TEST_F macros
- Mock dependencies using Google Mock
- Follow Given-When-Then naming
- Use fixtures for shared setup
- Clean up resources properly

### Test Naming

Use Given-When-Then format:

```text
GivenValidCredentials_WhenAuthenticating_ThenReturnsSuccess
GivenInvalidInput_WhenProcessing_ThenThrowsArgumentException
GivenExpiredToken_WhenRefreshing_ThenRenewsToken
```

### Test Organization

- Keep tests close to the code they test
- Group related tests in test classes
- Use test categories/traits for organization
- Separate unit tests from integration tests

## Security Standards

- **Never hardcode secrets**: Use configuration, environment variables, or secret management
- **Validate all inputs**: Especially from external sources
- **Use parameterized queries**: Prevent SQL injection
- **Sanitize output**: Prevent XSS attacks
- **Use HTTPS**: For all external communication
- **Hash passwords**: Use bcrypt, Argon2, or PBKDF2
- **Principle of least privilege**: Grant minimum necessary permissions

## Performance Guidelines

- **Profile before optimizing**: Don't guess, measure
- **Async for I/O**: Use async/await for database, network, file operations
- **Avoid N+1 queries**: Use eager loading or batch operations
- **Cache appropriately**: But consider cache invalidation complexity
- **Use appropriate data structures**: Dictionary for lookups, List for iteration, etc.

## Version Control

- **Write meaningful commit messages**: Explain why, not just what
- **Keep commits focused**: One logical change per commit
- **Reference issues**: Include issue numbers in commit messages
- **Don't commit secrets**: Use .gitignore for sensitive files
