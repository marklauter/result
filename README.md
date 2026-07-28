# Results

A `Result<T>` type for .NET that models domain failure as a value instead of an exception.

```bash
dotnet add package MSL.Results
```

Results targets .NET 10.

## Why

An operation that can fail in a way your domain cares about — input that doesn't parse, a withdrawal that exceeds the balance, a lookup that finds nothing — isn't exceptional. It's an outcome. Return it.

`Result<T>` is a closed hierarchy: `Success` and `Failure` are its only inhabitants, and the base constructor is `private protected`, so nothing outside the assembly can join them. The combinators are abstract on the base and implemented on each inhabitant, which makes exhaustiveness a compile-time fact. Add an inhabitant and the code stops compiling, whereas a `switch` expression would only warn.

Exceptions still have a job. Keep them for the substrate failing or the code being wrong — a dropped connection, a missing connection string, a null argument from a caller you don't control.

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
    Result.Validate(name.Length > 0, Error.Validation("name.empty", "Name is required.")),
    Result.Validate(age >= 18, Error.Validation("age.minor", "Must be 18 or older.")),
    Result.Validate(email.Contains('@'), Error.Validation("email.malformed", "Email is malformed.")));
```

All three violations come back together, in input order.

`Sequence` is the collection-shaped counterpart. A batch of parses reports every bad row in one pass:

```csharp
Result<ImmutableArray<Sku>> skus = lines.Select(Sku.Parse).Sequence();
```

The two-argument `Apply` feeds a wrapped argument to a wrapped function. Curry the constructor and apply once per argument, so a value built from several independent parses still collects every error:

```csharp
Result<Address> address = Result.Apply(
    Result.Apply(
        Result.Success((Street s) => (City c) => new Address(s, c)),
        Street.Parse(streetInput)),
    City.Parse(cityInput));
```

## Errors

`Error` is a readonly record struct carrying a typed category (`Type`), a stable machine-readable code, and a human-readable message. The category is what your caller branches on — map it to a status code at the boundary and leave the core transport-agnostic.

```csharp
Error.Validation("order.qty_invalid", "Quantity must be positive.");
Error.NotFound("order.missing", $"No order with id {id}.");
Error.Gone("order.purged", "The order was purged after 7 years.");
Error.Conflict("order.already_shipped", "The order has already shipped.");
Error.Undefined("order.unknown", "Unclassified order failure.");
```

Construction goes through the factories, which reject a null, empty, or whitespace code or message. A `default(Error)` is a bug rather than a valid value, so reading its `Code` or `Message` throws `InvalidOperationException` instead of handing you a null through a non-nullable declaration.

## API

| Member | What it does |
| --- | --- |
| `Result.Success(value)` | Wraps a value. Infers `T`. |
| `Result.Failure<T>(error)` | Wraps one error. `T` is explicit — it can't be inferred. |
| `Result.Failure<T>(errors)` | Wraps many. Overloads for `ReadOnlySpan`, `ImmutableArray`, and `IReadOnlyList`. |
| `Result.Validate(condition, error)` | Lifts a bool check to `Result<Unit>`. The entry point for accumulation. |
| `Result.Apply(fn, arg)` | Applicative application. Accumulates errors from both sides. |
| `Result.Apply(results)` | Variadic sequencing over `Result<Unit>`. |
| `Map` / `Select` | Transforms the success value. |
| `Bind` / `SelectMany` | Chains a fallible step. Short-circuits. |
| `BindAsync`, `MapAsync`, `MatchAsync` | Carry a chain through `ValueTask<Result<T>>`. |
| `Match` | Folds both paths to a value. |
| `Sequence` | Turns `IEnumerable<Result<T>>` into `Result<ImmutableArray<T>>`. |

A `Failure` always carries at least one error: the factories enforce it, and the inhabitant's constructor is internal, so there's no way around them. `Failure` equality is structural over the errors, element-wise and order-sensitive.

## License

MIT. See [LICENSE](LICENSE).
