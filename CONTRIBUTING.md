# Contributing to ARENA Renderfusion

The general Contribution Guide for all ARENA projects can be found [here](https://docs.arenaxr.org/content/contributing.html).

This document covers **development rules and conventions** specific to this repository. These rules are mandatory for all contributors, including automated/agentic coding tools.

## Development Rules

### 1. MQTT Topics — Always Use the `TOPICS` Constructor

**Never hardcode MQTT topic strings.** All topic paths must be constructed using the local `TOPICS` string constructor for ease of future topics modulation. This enables future topic format refactoring without scattered string updates.

### 2. Dependencies — Pin All Versions

**All dependencies must use exact, pegged versions** (no `^`, `~`, or `*` ranges). This prevents version drift across environments and ensures reproducible builds for security.

## Local Development

To develop `arena-renderfusion` locally:
1. Install standard C++/CMake build tools.
2. Execute the setup scripts or use standard `cmake .. && make` pipelines to build the application.
3. Ensure the `config.json` uses the correct endpoints.

## Code Style
- Follow standard C++ coding conventions.
- Provide explicit error trapping for 3D streaming sockets.

The `arena-renderfusion` uses [Release Please](https://github.com/googleapis/release-please) to automate CHANGELOG generation and semantic versioning. Your PR titles *must* follow Conventional Commit standards (e.g., `feat:`, `fix:`, `chore:`).
