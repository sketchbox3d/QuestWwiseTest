# Changelog

Notable changes to this repository.

## Unreleased

### Added
- `com.sketchbox.logging` package: logging facade and global exception handler.
- Editor tests covering the logging facade.
- CI workflow running repository hygiene checks.
- `.editorconfig` defining formatting and compiler diagnostic severities.
- `LICENSE`.

### Notes
- No call sites were migrated to `Sketchbox.Logging.Log`. This repository contains
  no first-party C#; all code under `Assets/` is vendored Oculus and Wwise SDK and
  is left unchanged.
