# Parking Garage Attendant Console - .NET 6

## Architecture

- Controller - menu flow and user actions
- View - console display
- Service - business rules and asynchronous workflows
- Repository - in-memory parking/history/rule state
- FileHelper - asynchronous CSV and audit-file I/O
- Core/Model - domain models
- Core/Constant - enums
- Core/Interface - service/repository contracts
- Helper - validation and cancellable console input wrapper

## Important .NET 6 note
.NET 6 does not expose `TextReader.ReadLineAsync(CancellationToken)`. The project therefore isolates console input in `InputHelper.ReadLineAsync(CancellationToken)`, using .NET 6's `ReadLineAsync()` together with cancellation observation. The rest of the application uses cancellation-aware async APIs where .NET 6 provides them.

## Run

```bash
dotnet restore
dotnet run
```

Default capacities are 20 / 15 / 10. The overstay threshold defaults to 4 hours.
