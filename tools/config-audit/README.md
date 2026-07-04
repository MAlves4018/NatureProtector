# Configuration authority validator

This tool verifies the Phase 2 ownership boundaries without executing project
scripts or contacting external services.

```bash
python tools/config-audit/validate.py --repo .
python -m unittest discover -s tools/config-audit/tests -p "test_*.py"
```

It checks:

- the machine-readable authority registry;
- common deployment configuration versus environment overlays;
- removal of superseded configuration sources;
- central NuGet package ownership;
- shared Python dependency pins;
- existence of referenced schemas and authority files.

A missing static reference is never interpreted as proof that code is dead.
