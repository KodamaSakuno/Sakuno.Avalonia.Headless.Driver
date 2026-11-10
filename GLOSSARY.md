# Avalonia.Headless.Driver

Domain language for coding and testing Agents operating Avalonia.Headless logical UIs, scoped to automated observation, action, and verification.

## Language

**Agent**:
A software agent that can invoke tools and automatically execute and verify Avalonia UI tests.
_Avoid_: Human tester, general-purpose desktop RPA

**Agent Skill**:
Operational guidance that teaches an Agent how to start and use the driver to complete the observation-action-verification workflow; it is not a UI automation capability itself.
_Avoid_: Runtime agent, UI driver protocol

**Headless Host**:
The runtime entity that hosts a Headless Session and provides Agents with a local entry point for operating it.
_Avoid_: Regular desktop application, remote UI service

**Headless Session**:
A logical UI runtime context hosted by Avalonia.Headless that an Agent can observe and operate; it does not correspond to a real desktop window or an operating-system input session.
_Avoid_: Desktop session, browser session

**Startup Adapter**:
The test startup composition owned by the project under test, responsible for establishing the application, test services, and initial UI state; it is not the production application entry point.
_Avoid_: Production Program.Main, automatically inferred application entry point

**Observation**:
A description of identifiable controls and their states within a Headless Session, used by an Agent to decide subsequent actions and determine results.
_Avoid_: Pixel recognition, operating-system screenshot

**Visual Observation**:
An image-based observation of a Headless window's actual rendered output, supplementing the Semantic Tree; it is not the primary basis for control location or pixel recognition.
_Avoid_: Operating-system screenshot, image-driven automation

**Semantic Tree**:
A filtered control hierarchy and its user-perceivable semantics, serving as the primary organization of an Observation; it is not the complete Avalonia object graph.
_Avoid_: Raw object graph, full visual-tree dump

**Element Query**:
Repeatable criteria for relocating a target control; an ambiguous match requires further disambiguation rather than defaulting to the first result.
_Avoid_: Long-lived object reference, fixed tree index

**UI Action**:
A verifiable operation performed by an Agent on the logical UI, primarily expressing user intent, with low-level input serving only as a supplement.
_Avoid_: Blind input injection

**Verification**:
The use of Observations to determine whether the UI under test meets expectations and, when it does not, to form an explainable failure conclusion.
_Avoid_: Execution without checking the result
