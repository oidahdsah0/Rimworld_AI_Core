# Contributing to RimWorld AI Core

We love your input! We want to make contributing to RimWorld AI Core as easy and transparent as possible, whether it's:

- Reporting a bug
- Discussing the current state of the code
- Submitting a fix
- Proposing new features
- Becoming a maintainer

## We Develop with Github

We use github to host code, to track issues and feature requests, as well as accept pull requests.

## We Use [Github Flow](https://guides.github.com/introduction/flow/index.html)

Pull requests are the best way to propose changes to the codebase. We actively welcome your pull requests:

1. Fork the repo and create your branch from `main`.
2. If you've added code that should be tested, add tests.
3. If you've changed APIs, update the documentation.
4. Ensure the test suite passes.
5. Make sure your code lints.
6. Issue that pull request!

## Any contributions you make will be under the MIT Software License

In short, when you submit code changes, your submissions are understood to be under the same [MIT License](http://choosealicense.com/licenses/mit/) that covers the project. Feel free to contact the maintainers if that's a concern.

## Report bugs using Github's [issue tracker](https://github.com/oidahdsah0/Rimworld_AI_Core/issues)

We use GitHub issues to track public bugs. Report a bug by [opening a new issue](https://github.com/oidahdsah0/Rimworld_AI_Core/issues/new).

## Write bug reports with detail, background, and sample code

**Great Bug Reports** tend to have:

- A quick summary and/or background
- Steps to reproduce
  - Be specific!
  - Give sample code if you can
- What you expected would happen
- What actually happens
- Notes (possibly including why you think this might be happening, or stuff you tried that didn't work)

## Development Setup

### Prerequisites

- Visual Studio 2019 or later (or VS Code with C# extension)
- .NET Framework 4.7.2 SDK
- RimWorld 1.6+ (for testing)
- RimAI Framework (dependency)

### Setting Up Development Environment

1. Clone the repository:
   ```bash
   git clone https://github.com/oidahdsah0/Rimworld_AI_Core.git
   cd Rimworld_AI_Core
   ```

2. Ensure you have the RimAI Framework project available (either as a submodule or adjacent directory)

3. Open `Rimworld_AI_Core.sln` in Visual Studio

4. Build the solution to restore dependencies

5. Configure your RimWorld installation path in the project properties for testing

### Code Style

- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise
- Use regions to organize code logically

### Testing

- Test your changes in RimWorld before submitting
- Ensure compatibility with the latest RimAI Framework
- Test with different LLM providers if possible
- Check for memory leaks and performance issues

### Submitting Changes

1. Create a feature branch from `main`
2. Make your changes
3. Test thoroughly
4. Update documentation if needed
5. Submit a pull request with:
   - Clear description of what was changed
   - Why the change was necessary
   - Any breaking changes
   - Screenshots/videos if UI changes

## Code of Conduct

### Our Pledge

We as members, contributors, and leaders pledge to make participation in our
community a harassment-free experience for everyone, regardless of age, body
size, visible or invisible disability, ethnicity, sex characteristics, gender
identity and expression, level of experience, education, socio-economic status,
nationality, personal appearance, race, religion, or sexual identity
and orientation.

### Our Standards

Examples of behavior that contributes to a positive environment include:

- Using welcoming and inclusive language
- Being respectful of differing viewpoints and experiences
- Gracefully accepting constructive criticism
- Focusing on what is best for the community
- Showing empathy towards other community members

Examples of unacceptable behavior include:

- The use of sexualized language or imagery, and sexual attention or advances of any kind
- Trolling, insulting or derogatory comments, and personal or political attacks
- Public or private harassment
- Publishing others' private information without explicit permission
- Other conduct which could reasonably be considered inappropriate in a professional setting

### Enforcement

Instances of abusive, harassing, or otherwise unacceptable behavior may be
reported to the community leaders responsible for enforcement at
[GitHub Issues](https://github.com/oidahdsah0/Rimworld_AI_Core/issues).

All complaints will be reviewed and investigated promptly and fairly.

## License

By contributing, you agree that your contributions will be licensed under its MIT License.

## References

This document was adapted from the open-source contribution guidelines for [Facebook's Draft](https://github.com/facebook/draft-js/blob/a9316a723f9e918afde44dea68b5f9f39b7d9b00/CONTRIBUTING.md).
