using System.Text;
using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Tests;

public sealed class ContractCosmosSerializerTests
{
    private readonly ContractCosmosSerializer _serializer = new();

    [Fact]
    public void A_stream_asked_for_as_a_stream_is_returned_open()
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        using var output = _serializer.FromStream<Stream>(input);

        output.CanRead.ShouldBeTrue();
        new StreamReader(output).ReadToEnd().ShouldBe("{}");
    }

    [Fact]
    public void A_document_round_trips_with_the_contract_options_and_its_stream_is_disposed()
    {
        var stream = _serializer.ToStream(Sample.Stored);

        var back = _serializer.FromStream<StoredMatch>(stream);

        back.MatchId.ShouldBe(Sample.Stored.MatchId);
        back.Status.ShouldBe(Sample.Stored.Status);
        stream.CanRead.ShouldBeFalse();
    }
}
