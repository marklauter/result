[![.NET Tests](https://github.com/marklauter/result/actions/workflows/dotnet.tests.yml/badge.svg)](https://github.com/marklauter/result/actions/workflows/dotnet.tests.yml)
[![.NET Publish](https://github.com/marklauter/result/actions/workflows/dotnet.publish.yml/badge.svg)](https://github.com/marklauter/result/actions/workflows/dotnet.publish.yml)
[![NuGet](https://img.shields.io/nuget/v/MSL.Results?logo=nuget)](https://www.nuget.org/packages/MSL.Results/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0/)

![MSL Armory](https://raw.githubusercontent.com/marklauter/result/main/images/msl.armory.small.png "MSL Armory")

# Results

*Another weapon from the MSL Armory*

A `Result<T>` type for .NET that models domain failure as a value instead of an exception.

```bash
dotnet add package MSL.Results
```

Results targets .NET 10.

## Why

An operation that can fail in a way your domain cares about isn't exceptional. It's an outcome. Return it.

`Result<T>` is a closed hierarchy: `Success` and `Failure` are its only inhabitants, and the base constructor is `private protected`, so nothing outside the assembly can join them. Each combinator is abstract on the base and overridden by both inhabitants. A non-virtual public method wraps each one and guards its arguments once, so the contract cannot diverge by inhabitant. Exhaustiveness is then a compile-time fact: add an inhabitant and the code stops compiling, whereas a `switch` expression would only warn.

## Result or exception?

Both channels exist in .NET, and the usual advice doesn't separate them.

- *Is it expected?* Everything is expected to someone. `HttpClient` throws on socket errors, and anyone who has used the internet expects socket errors.
- *Can the caller branch on it?* You can always branch. A `catch` filter branches on an exception as readily as `Match` branches on a `Result`.
- *Can the caller recover?* `HttpClient` throws, and the caller can still retry, fall back, or degrade.

All three test the caller's options, which is why they produce case-by-case argument that never settles. The line is structural.

Model the operation as a total function and ask where the outcome lives.

**In the codomain** — an outcome the operation's own logic produces. Input that doesn't parse, a withdrawal exceeding the balance, a lookup finding nothing. Return it as a `Result<T>`. The function stays total: every input maps to a modeled outcome, and nothing escapes out of band.

**Outside it** — a failure in the machinery the operation assumes. Memory, network, disk, configuration, or a caller handing you a null. Throw. The event was never part of the mapping, so an exception is the honest signal.

An exception is out of band and lies about totality. A `Result` is in band and lets the compiler force exhaustive handling. That is why modeled outcomes belong in the return type.

### Where a `Result` comes from

Only two places.

1. **At the boundary**, parsing external input into a domain type. That is the one place invalid states are still representable, so the one place a domain invariant can fail to hold.
2. **At a business branch point**, expressed as a sum type. `Withdraw(amount)` returning success or `InsufficientFunds` is a decision, not an invalid state. The function is still total.

Make invalid states unrepresentable and the interior comes out total for free. A value that exists is valid by construction, so a pure domain function over valid values cannot produce an invariant violation. The interior never mints an error and never throws.

### What is left to throw

Three things.

- **Transient infrastructure faults**, effectively every timeout and partition. You can never know a database is down, only decide to stop waiting. That is what keeps it out of the codomain. Retry, and propagate when the budget runs out.
- **Permanent infrastructure faults**, meaning configuration. The missing connection string, the IAM error on a resource you need. Fail fast and loud at startup.
- **Bugs**, meaning a violated precondition, API misuse, or a broken assertion. You fix these rather than handle them.

Cancellation is thrown, never returned. `OperationCanceledException` is cooperative control flow, not an outcome of the operation.

Adapters below the domain are allowed to throw, and they translate at the edge. A query's row-not-found becomes a `NotFound` error; a dropped connection stays an exception and propagates. The pure core never sees a raw `SqlException`. This is also why `HttpClient` throwing is correct rather than a counterexample: from your layer it is infrastructure, and a socket fault is its substrate failing. Domain is relative to layer, so the rule reapplies at each one — every layer's core is total, and every layer's adapters throw.

Scott Wlaschin's [*Domain Modeling Made Functional*](https://www.amazon.com/dp/B0CY2L7Y1K) is the book-length version of this argument. It builds Eric Evans's domain-driven design in F# out of sum types, total functions, and errors carried as values.

## Compose the happy path

`Map` transforms a success and passes a failure through. `Bind` chains another fallible step and short-circuits. `Match` is how you leave the type: `Result<T>` exposes no value of its own, so both paths get handled on the way out.

```csharp
Result<Order> order = ParseOrderId(input)
    .Bind(LoadOrder)
    .Map(o => o with { Status = Status.Confirmed });

string message = order.Match(
    o => $"Confirmed {o.Id}.",
    errors => string.Join("; ", errors.Select(e => e.Message)));
```

`Select` and `SelectMany` are LINQ-named aliases of the same operations, so query syntax works:

```csharp
var total = from cart in LoadCart(cartId)
            from rate in LoadTaxRate(cart.Region)
            select cart.Subtotal * (1 + rate);
```

Once a step is asynchronous, the chain continues through the `ValueTask<Result<T>>` extensions:

```csharp
Result<Receipt> receipt = await ParseOrderId(input)
    .BindAsync(LoadOrderAsync)
    .BindAsync(ChargeAsync)
    .MapAsync(ToReceipt);
```

`ValueTask` is the async currency throughout, so a continuation that completes synchronously allocates nothing. The API threads no `CancellationToken` — pass a lambda that captures the token from the enclosing scope.

## Accumulate every error

`Bind` is sequential: it reports the first failure and stops. When the failures are independent, you want all of them. `Apply` and `Sequence` collect them.

Lift each check with `Validate`, then combine:

```csharp
Result<Unit> valid = Result.Apply(
    Result.Validate(!string.IsNullOrWhiteSpace(name), Error.Validation(ErrorCode.Unchecked("name.empty"), ErrorMessage.Unchecked("Name is required."))),
    Result.Validate(age >= 18, Error.Validation(ErrorCode.Unchecked("age.minor"), ErrorMessage.Unchecked("Must be 18 or older."))),
    Result.Validate(email is not null && email.Contains('@'), Error.Validation(ErrorCode.Unchecked("email.malformed"), ErrorMessage.Unchecked("Email is malformed."))));
```

All three violations come back together, in input order.

`Sequence` is the collection-shaped counterpart. A batch of parses reports every bad row in one pass:

```csharp
Result<ImmutableArray<Sku>> skus = lines.Select(Sku.Checked).Sequence();
```

The two-argument `Apply` feeds a wrapped argument to a wrapped function. Curry the constructor and apply once per argument, so a value built from several independent parses still collects every error:

```csharp
Result<Address> address = Result.Apply(
    Result.Apply(
        Result.Success((Street s) => (City c) => new Address(s, c)),
        Street.Checked(streetInput)),
    City.Checked(cityInput));
```

### Gate, then accumulate

`Validate` takes a bool, and C# evaluates arguments before the call. Every check handed to an `Apply` therefore runs, including the ones an earlier check was meant to make safe. A precondition cannot be a peer of the checks it guards: put a null test beside the pattern tests that assume a non-null input, and the pattern test throws `ArgumentNullException` out of a function whose whole purpose is returning failure as a value.

`Bind` is the gate: it short-circuits, so nothing downstream runs on an input the precondition rejected. `Apply` then accumulates the independent checks that follow. One chain, both combinators, each doing the job it is for. This is the `Sku.Checked` that the batch parse above calls:

```csharp
public static Result<Sku> Checked(string input) =>
    Result.Validate(
        !string.IsNullOrWhiteSpace(input),
        Error.Validation(ErrorCode.Unchecked("sku.empty"), ErrorMessage.Unchecked("SKU is required.")))
    .Bind(_ => Result.Apply(
        Result.Validate(
            SkuPatterns.Allowed().IsMatch(input),
            Error.Validation(ErrorCode.Unchecked("sku.disallowed"), ErrorMessage.Unchecked("SKU allows only A-Z, 0-9, and hyphen."))),
        Result.Validate(
            !SkuPatterns.BoundaryHyphen().IsMatch(input),
            Error.Validation(ErrorCode.Unchecked("sku.boundary_hyphen"), ErrorMessage.Unchecked("SKU cannot start or end with a hyphen.")))))
    .Map(_ => new Sku(input));
```

The two pattern checks are independent of each other: both need a non-null input and nothing more, which the gate has already established. Binding them would report the disallowed character and hide the boundary hyphen, so the caller fixes one, resubmits, and learns about the other — the round trip `Apply` exists to remove. Accumulation only pays off when the accumulated errors carry distinct codes: two failures sharing one `sku.invalid` leave the caller unable to tell them apart except by the message text.

## Errors

`Error` is a readonly record struct carrying a typed category (`Type`), a stable machine-readable code, and a human-readable message. The category is what your caller branches on — map it to a status code at the boundary and leave the core transport-agnostic.

```csharp
Error.Validation(ErrorCode.Unchecked("order.qty_invalid"), ErrorMessage.Unchecked("Quantity must be positive."));
Error.NotFound(ErrorCode.Unchecked("order.missing"), ErrorMessage.Unchecked($"No order with id {id}."));
Error.Gone(ErrorCode.Unchecked("order.purged"), ErrorMessage.Unchecked("The order was purged after 7 years."));
Error.Conflict(ErrorCode.Unchecked("order.already_shipped"), ErrorMessage.Unchecked("The order has already shipped."));
```

The factories take only the typed wrappers, so a code and a message can never be transposed at a call site. Lifting a string is the caller's act: `Checked` is the fallible lift, returning `Result<ErrorCode>`/`Result<ErrorMessage>` and rejecting null, empty, and whitespace; `Unchecked` is the total embedding for values you vouch for, such as the literals above. A `default(Error)` is a bug rather than a valid value, so reading its `Code` or `Message` throws `InvalidOperationException` instead of handing you a null through a non-nullable declaration.

## API

| Member | What it does |
| --- | --- |
| `Result.Success(value)` | Wraps a value. Infers `T`. |
| `Result.Success()` | The `Unit` success for a void-shaped operation. |
| `Result.Failure<T>(error)` | Wraps one error. `T` is explicit — it can't be inferred. |
| `Result.Failure<T>(errors)` | Wraps many. Overloads for `ReadOnlySpan`, `ImmutableArray`, and `IReadOnlyList`. |
| `Result.Validate(condition, error)` | Lifts a bool check to `Result<Unit>`. The entry point for accumulation. |
| `Result.Apply(fn, arg)` | Applicative application. Accumulates errors from both sides. |
| `Result.Apply(results)` | Variadic sequencing over `Result<Unit>`. |
| `Map` / `Select` | Transforms the success value. |
| `Bind` / `SelectMany` | Chains a fallible step. Short-circuits. |
| `BindAsync`, `MapAsync`, `MatchAsync` | Carry a chain through `ValueTask<Result<T>>`. |
| `Match` | Folds both paths to a value. |
| `Sequence` | Turns a collection of `Result<T>` into `Result<ImmutableArray<T>>`. Overloads for `IEnumerable`, `ReadOnlySpan`, and `ImmutableArray`. |

A `Failure` always carries at least one error: the factories enforce it, and the inhabitant's constructor is internal, so there's no way around them. `Failure` equality is structural over the errors, element-wise and order-sensitive, because the error collection is library-owned.

`Success` equality is the payload's. It delegates to `EqualityComparer<T>.Default`, so two successes are equal exactly when their values are, and a payload that compares by reference keeps comparing by reference here. `Sequence` returns `Result<ImmutableArray<T>>`, and `ImmutableArray<T>` compares its underlying array by reference, so two structurally identical successes are not `==`. Compare those payloads with `SequenceEqual` through `Match`.

---
[Repository](https://github.com/marklauter/result) · [NuGet](https://www.nuget.org/packages/MSL.Results/) · [MIT License](https://github.com/marklauter/result/blob/main/LICENSE) · [Report an issue](https://github.com/marklauter/result/issues)
