using System.Runtime.CompilerServices;

// Unity compiles editor tests separately from the pure Domain assembly.
// Keep implementation details internal; grant access only to the existing test assemblies.
[assembly: InternalsVisibleTo("Dravio.Domain.Tests")]
[assembly: InternalsVisibleTo("Dravio.Persistence.Tests")]
