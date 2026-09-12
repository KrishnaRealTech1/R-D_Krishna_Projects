# Build fix - CS8864 EventArgs inheritance

The initial iAWS v2.0 source declared three event payload types as C# records inheriting from `System.EventArgs`.
C# records may inherit only from `object` or another record, while `EventArgs` is a normal class. This caused the publish errors:

- CS8864
- CS0115 for EqualityContract
- CS0115 for Equals(EventArgs?)
- CS0115 for PrintMembers(StringBuilder)

Fixed in `src/RfidVehicleAccess.App/Models/IawsModels.cs` by converting these three types to sealed classes derived from `EventArgs` while keeping the same constructor signatures and public property names:

- `WeightChangedEventArgs(decimal weightKg, DateTimeOffset timestamp)`
- `RfidReadEventArgs(string rfid, DateTimeOffset timestamp)`
- `IawsCameraStatusChangedEventArgs(IawsCameraPosition position, string status)`

No call-site changes are required because the existing `new ...(...)` constructor usage and `EventHandler<TEventArgs>` subscriptions remain compatible.
