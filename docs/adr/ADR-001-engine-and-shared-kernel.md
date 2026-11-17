# ADR-001: Unity 6.3 LTS, shared simulation kernel, per-platform adapters

Status: accepted (2026-11-17)

## Decision
- Engine: Unity 6.3 LTS (patch pinned in ProjectSettings/ProjectVersion.txt once installed).
- Simulation/geometry rules live in one Unity-independent C# assembly (`ForgeBorn.Domain`, netstandard2.1, C# 9).
- iOS and Android differ only in rendering profile, build pipeline, haptics adapter and tuning (Platform/IOS, Platform/Android).
- Domain uses its own double-precision vector types, not UnityEngine.Vector3 (deviation from design doc section 5 sketch).

## Why
- Two separate physics implementations would diverge and double QA cost.
- A Unity-free Domain runs under `dotnet test` in seconds, without the Editor.

## Consequences
- A platform-specific kernel is allowed only behind a stable interface with golden-test parity, after benchmarks.
- iOS status stays "unverified" until built with Xcode and run on an iPhone.
