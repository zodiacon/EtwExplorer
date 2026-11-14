# 05-nullable-and-csharp-modernization: Enable nullable reference types and modernize C# usage

With the solution green on .NET 11, enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in both projects and resolve every resulting warning properly (annotate nullable members, add guards, avoid `!` except where justified). Apply low-risk C# 15-era modernizations across the ~1.6k LOC: file-scoped namespaces, `using` cleanup, target-typed `new`, collection expressions, pattern matching, and `sealed` on view models where appropriate — without changing behaviour.

Research with the `modernizing-csharp-version` and `migrating-csharp-nullable-references` skills before editing. Keep MVVM semantics intact (Prism `BindableBase.SetProperty` patterns, `DelegateCommand.ObservesProperty`).

**Done when**: Both projects have nullable and implicit usings enabled, the solution builds with zero errors and zero warnings, and the app still launches and performs the open-manifest flow.
