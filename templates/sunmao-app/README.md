# Sunmao application template

This `dotnet new` template creates a small WPF application with one simulated Component, a private
poller, an application-owned lifecycle coordinator, a shared UI pulse, an asynchronous command and
the Sunmao theme. The generated test project exercises the Component without hardware.

The generated projects reference versioned `Sunmao.*` packages, currently `0.2.0`, and support
`net8.0-windows` and `net10.0-windows`. To verify the template from this repository, pack the
required libraries into a local NuGet source first, then pass that source to `dotnet restore` in the
generated directory. A package feed is deliberately not hard-coded into the generated application.

Example:

```text
dotnet new install .\templates\sunmao-app
dotnet new sunmao-app -n SampleApp
dotnet restore SampleApp --source <local-or-remote-sunmao-feed>
dotnet build SampleApp
dotnet test SampleApp
```

The template is a composition example, not a domain application. Replace the simulated Component
with an application-owned Component after the device contract and shutdown behavior are defined.
