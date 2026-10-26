---
name: en-skill
description: "Local development rules: paths and usage rules for the local Python, C++ and Node.js/pnpm toolchains, and where AI-generated files are stored and how they are managed."
---

# En-SKILL: Local Development and Output Rules

Applies to engineering work, toolchain usage and output management on this machine. Paths, tools and practices explicitly specified by the user take precedence; existing project structure and configuration are kept as they are. Only apply the parts relevant to the current task.

## 1. Toolchains

| Toolchain | Path / command | Notes |
| :--- | :--- | :--- |
| Python 3.13 (py13) | `E:\minconda\envs\py13\python.exe` | Conda root is `E:\minconda`; use the project's own virtual environment if it has one |
| C++ (MSYS2 UCRT64) | `E:\msys2\ucrt64\bin` | Provides `gcc`, `g++`, `cmake`, `ninja`; keep the project's existing build configuration and do not mix in other compiler toolchains |
| Node.js | `E:\nodejs\node.exe` | Local runtime |
| pnpm | `%APPDATA%\npm\pnpm.cmd` | Use when the project uses pnpm or has a `pnpm-lock.yaml`; follow the project's convention for other lock files |

### Usage rules

- Call Python by absolute path, and check or install dependencies with `-m pip` of the same interpreter so nothing lands in another environment.
- py13 has no third-party packages preinstalled. When a dependency is missing, prefer the standard library or existing tools first; if installation is really needed, install from the project's dependency list or as the user instructs.
- When MSYS2 or Node.js/pnpm is needed, change `$env:PATH` only in the current terminal; never modify system or user environment variables.
- If a path is invalid or a tool is missing, verify its actual location first; without the user's consent, do not keep retrying the old path or rebuild the environment.

```powershell
# Python
& 'E:\minconda\envs\py13\python.exe' --version
& 'E:\minconda\envs\py13\python.exe' -m pip show '<package_name>'

# C++ (current terminal only)
$env:PATH = "E:\msys2\ucrt64\bin;$env:PATH"

# Node.js / pnpm (current terminal only)
$env:PATH = "E:\nodejs;$env:APPDATA\npm;$env:PATH"
```

## 2. Files and Output

Unless specified otherwise, AI-generated auxiliary documents, reports and intermediate files go under `AI/` in the workspace root, not in the project root or the business source directories:

```text
AI/
├── docs/       # Implementation plans, design notes
├── reports/    # Summaries or analysis reports requested by the user
└── temp/       # Temporary data, debug logs, intermediate artifacts
```

- Source code, tests, configuration and build output stay in the project's existing directories; locations specified by the user take precedence.
- Edit existing files in place, keeping unrelated code, comments and formatting; do not move, rename or reorganize existing files just to "tidy up".
- Give files under `AI/` names that describe their purpose, such as `AI/docs/architecture_plan.md`.
- Before delivery, delete files in `AI/temp/` created by this task that are no longer needed; never delete original project files, other people's files, or results that still need to be delivered.
- Before deleting non-temporary files, overwriting existing results, or changing global configuration, make sure the user has authorized it.
