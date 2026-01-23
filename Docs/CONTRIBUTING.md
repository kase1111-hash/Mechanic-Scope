# Contributing to Mechanic Scope

Thank you for your interest in contributing to Mechanic Scope! This document provides guidelines and instructions for contributing to the project.

---

## Table of Contents

- [Getting Started](#getting-started)
- [Development Environment](#development-environment)
- [Code Style](#code-style)
- [Submitting Changes](#submitting-changes)
- [Types of Contributions](#types-of-contributions)
- [Testing](#testing)
- [Documentation](#documentation)
- [Community Guidelines](#community-guidelines)

---

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Mechanic-Scope.git
   cd Mechanic-Scope
   ```
3. **Add the upstream remote**:
   ```bash
   git remote add upstream https://github.com/kase1111-hash/Mechanic-Scope.git
   ```
4. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

---

## Development Environment

### Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| Unity | 2022.3 LTS | [Unity Hub](https://unity.com/download) |
| Visual Studio or VS Code | Latest | [VS Code](https://code.visualstudio.com/) |
| Git | 2.x+ | [Git](https://git-scm.com/) |

### Unity Setup

1. Open Unity Hub
2. Add the cloned project folder
3. Open with Unity 2022.3 LTS
4. Wait for package resolution to complete

See [UNITY_SETUP.md](./UNITY_SETUP.md) for detailed configuration instructions.

### Required Packages

These packages should auto-import from the project manifest:
- AR Foundation 5.x
- ARCore XR Plugin
- ARKit XR Plugin
- Universal Render Pipeline 14.x
- TextMeshPro

---

## Code Style

### C# Conventions

- **Naming**: PascalCase for public members, camelCase for private fields
- **Prefixes**: Use `_` prefix for private fields (e.g., `_partDatabase`)
- **Braces**: Allman style (opening brace on new line)
- **Indentation**: 4 spaces (no tabs)
- **Line length**: 120 characters max

### Example

```csharp
public class PartDatabase : MonoBehaviour
{
    [SerializeField]
    private SQLiteConnection _database;

    private Dictionary<string, PartInfo> _partCache;

    public PartInfo GetPart(string partId)
    {
        if (string.IsNullOrEmpty(partId))
        {
            throw new ArgumentNullException(nameof(partId));
        }

        if (_partCache.TryGetValue(partId, out var cachedPart))
        {
            return cachedPart;
        }

        return LoadPartFromDatabase(partId);
    }

    private PartInfo LoadPartFromDatabase(string partId)
    {
        // Implementation
    }
}
```

### Unity-Specific Guidelines

- Use `[SerializeField]` for private fields exposed in the Inspector
- Avoid `public` fields; use properties or `[SerializeField]`
- Cache component references in `Awake()` or `Start()`
- Use object pooling for frequently instantiated objects
- Prefer `async/await` with UniTask over coroutines for new code

### Comments and Documentation

- Use XML documentation for public APIs
- Add comments for complex logic, not obvious operations
- Keep comments up to date with code changes

```csharp
/// <summary>
/// Resolves step dependencies and returns all currently available steps.
/// </summary>
/// <param name="completedStepIds">IDs of steps already completed</param>
/// <returns>List of steps whose dependencies are satisfied</returns>
public List<ProcedureStep> GetAvailableSteps(HashSet<int> completedStepIds)
{
    // Implementation
}
```

---

## Submitting Changes

### Before Submitting

1. **Sync with upstream**:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Run tests**:
   ```bash
   ./run_tests.sh
   ```

3. **Test on device** if making AR-related changes

### Pull Request Process

1. Push your branch to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```

2. Open a Pull Request against the `main` branch

3. Fill out the PR template with:
   - Summary of changes
   - Related issue number (if applicable)
   - Testing performed
   - Screenshots for UI changes

4. Wait for code review and address feedback

### Commit Messages

Use clear, descriptive commit messages:

```
Add voice command support for step navigation

- Implement VoiceCommandManager with platform-specific recognizers
- Add "next step" and "go back" command handlers
- Include audio feedback for command recognition
```

Format:
- First line: imperative mood summary (50 chars max)
- Blank line
- Body: explain what and why (wrap at 72 chars)

---

## Types of Contributions

### Bug Fixes

1. Check existing issues to avoid duplicates
2. Create an issue describing the bug (if not exists)
3. Reference the issue in your PR

### New Features

1. **Open an issue first** to discuss the feature
2. Wait for maintainer feedback before implementing
3. Large features may require a design document

### Engine Models and Procedures

Community-contributed engine data is welcome! See:
- [ADDING_ENGINES.md](./ADDING_ENGINES.md) - How to add engine models
- [PROCEDURE_FORMAT.md](./PROCEDURE_FORMAT.md) - Procedure file specification

### Documentation

- Fix typos and improve clarity
- Add examples and tutorials
- Translate documentation (coordinate with maintainers)

---

## Testing

### Running Tests

```bash
# Run all tests
./run_tests.sh

# Or in Unity:
# Window → General → Test Runner
# Run All (Edit Mode and Play Mode)
```

### Test Categories

| Category | Location | Purpose |
|----------|----------|---------|
| Core | `Assets/Tests/Runtime/Core/` | Procedure engine, part database |
| Data | `Assets/Tests/Runtime/Data/` | Progress persistence |
| Voice | `Assets/Tests/Runtime/Voice/` | Voice command handling |
| Integration | `Assets/Tests/Runtime/Integration/` | End-to-end workflows |
| Performance | `Assets/Tests/Runtime/Performance/` | Performance benchmarks |
| Accessibility | `Assets/Tests/Runtime/Accessibility/` | Accessibility features |

### Writing Tests

- Add tests for new functionality
- Maintain or improve code coverage
- Use descriptive test names: `MethodName_Scenario_ExpectedResult`

```csharp
[Test]
public void GetAvailableSteps_WithCompletedDependency_ReturnsUnlockedStep()
{
    // Arrange
    var runner = new ProcedureRunner();
    runner.LoadProcedure(TestProcedure);

    // Act
    runner.CompleteStep(1);
    var available = runner.AvailableSteps;

    // Assert
    Assert.Contains(TestProcedure.Steps[1], available);
}
```

---

## Documentation

### Updating Documentation

- Update relevant docs when changing functionality
- Add new docs for new features
- Place documentation in the `Docs/` directory

### Documentation Style

- Use clear, concise language
- Include code examples where helpful
- Add diagrams for complex concepts
- Keep a consistent tone (professional but approachable)

---

## Community Guidelines

### Be Respectful

- Treat all contributors with respect
- Provide constructive feedback
- Assume good intentions

### Ask for Help

- Use GitHub Issues for questions
- Tag issues with `question` label
- Search existing issues before posting

### Report Security Issues

For security vulnerabilities, please see [SECURITY.md](../SECURITY.md) for reporting instructions. Do not open public issues for security problems.

---

## Recognition

Contributors are recognized in:
- Git commit history
- Release notes for significant contributions
- README acknowledgments for major features

---

## Questions?

- Open a GitHub Issue with the `question` label
- Check existing documentation in the `Docs/` folder
- Review closed issues for common questions

Thank you for contributing to Mechanic Scope!
