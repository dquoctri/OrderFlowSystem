namespace OrderFlow.Web.Api.Models;

/// <summary>The single origin the SPA talks to — the BFF. It composes the domain services.</summary>
public sealed record ApiUrls(string Bff);
