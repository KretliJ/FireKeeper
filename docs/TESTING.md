## 🧪 Testing

FireKeeper includes comprehensive testing:

* **28 Unit Tests** - Core logic coverage (backup rules, path helpers, profile parsing)
* **8 Integration Tests** - File system operations (backup creation, restore, sync folder)

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "TestCategory=Unit"

# Run only integration tests
dotnet test --filter "TestCategory=Integration"
```