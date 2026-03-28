# Contributing to ReportGen

Thank you for your interest in contributing! This guide covers everything you need to get started.

## Table of Contents
- [Getting Started](#getting-started)
- [Branch Naming](#branch-naming)
- [Commit Style](#commit-style)
- [Pull Request Process](#pull-request-process)
- [Code Standards](#code-standards)
- [Running Tests](#running-tests)

---

## Getting Started

1. **Fork** the repository on GitHub
2. **Clone** your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/ReportGen.git
   cd ReportGen
   ```
3. **Add upstream** remote:
   ```bash
   git remote add upstream https://github.com/puneet45678/ReportGen.git
   ```
4. **Restore** dependencies:
   ```bash
   dotnet restore
   ```
5. **Build** to verify everything works:
   ```bash
   dotnet build
   ```
6. **Create a branch** for your work (see naming below)

---

## Branch Naming

Use this format: `type/short-description`

| Type | When to use |
|------|-------------|
| `feat/` | New feature |
| `fix/` | Bug fix |
| `docs/` | Documentation only |
| `chore/` | Build, CI, config changes |
| `test/` | Adding or fixing tests |
| `refactor/` | Code refactoring |

Examples:
- `feat/pdf-exporter`
- `fix/csv-encoding-utf8`
- `docs/quickstart-guide`

---

## Commit Style

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short description>

[optional body]
[optional footer]
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

Examples:
```
feat(exporters): add Excel exporter with bold header row
fix(csv): handle null values in data columns
docs(readme): add quickstart example to README
chore(ci): update GitHub Actions to use .NET 8
```

---

## Pull Request Process

1. Ensure your branch is up-to-date with `main`
2. All tests pass: `dotnet test`
3. No new build warnings introduced
4. Add/update tests for your change
5. Update documentation if API changes
6. Fill in the PR template completely
7. Request review — at least one approval required before merge

---

## Code Standards

- **C# 10+** features are encouraged
- **Nullable reference types** must be enabled and respected
- **XML doc comments** on all public APIs (`///`)
- **No breaking changes** to public API without a major version bump
- Follow the [Microsoft C# coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

---

## Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific project
dotnet test tests/ReportGen.Tests/
```

---

## Reporting Issues

Use the GitHub issue templates:
- **Bug Report**: Something is broken
- **Feature Request**: New capability you want

Please search existing issues before creating a new one.

---

## Questions?

Open a [GitHub Discussion](https://github.com/puneet45678/ReportGen/discussions) for questions, ideas, and general discussion.
