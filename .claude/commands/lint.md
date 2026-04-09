Check v2 code formatting.

```bash
cd v2 && dotnet format --verify-no-changes
```

If formatting issues are found, fix them with:

```bash
cd v2 && dotnet format
```

Then re-run the check to verify.
