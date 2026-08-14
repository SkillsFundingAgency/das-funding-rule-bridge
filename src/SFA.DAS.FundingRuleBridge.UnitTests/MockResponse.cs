using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;

[assembly: ExcludeFromCodeCoverage]
namespace SFA.DAS.FundingRuleBridge.UnitTests;

public class MockResponse : Response
{
    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
    {
        throw new NotImplementedException();
    }

    protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        throw new NotImplementedException();
    }

    protected override bool ContainsHeader(string name)
    {
        throw new NotImplementedException();
    }

    protected override IEnumerable<HttpHeader> EnumerateHeaders()
    {
        throw new NotImplementedException();
    }

    public override int Status { get; }
    public override string ReasonPhrase { get; }
    public override Stream? ContentStream { get; set; }
    public override string ClientRequestId { get; set; }
}