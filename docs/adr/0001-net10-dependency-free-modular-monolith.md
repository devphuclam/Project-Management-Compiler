---
status: accepted
---

# Use the installed .NET 10 runtime for a dependency-free modular monolith

The compiler targets `net10.0` with ASP.NET Core and platform libraries only. .NET 10 is already installed and has a longer supported horizon in this environment than .NET 8, while the restricted workstation forbids installing packages or runtimes. A modular monolith keeps the canonical model and deterministic management engine in-process, with source and output adapters at explicit seams; a database, microservices, third-party UI framework, and third-party Excel library are deferred.
