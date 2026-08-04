using FluentAssertions;
using GmailPipeline.Core.Api;
using GmailPipeline.Core.Application.Parsing;
using GmailPipeline.Core.Contract.Models;
using GmailPipeline.Core.Contract.Parsing;

namespace GmailPipeline.Core.Test.Parsing;

public sealed class EmailPipelineTests
{
    [Fact]
    public async Task PipelineUsesHighestPriorityParserThatCanParse()
    {
        var parser = new StubParser("chosen", priority: 20, canParse: true, "value");
        var pipeline = new EmailPipeline<string>(new EmailParserResolver<string>(
        [
            new StubParser("lower", priority: 1, canParse: true, "wrong"),
            parser
        ]));

        var result = await pipeline.ProcessAsync(CreateMessage());

        result.ParserName.Should().Be("chosen");
        result.ParseResult.IsSuccess.Should().BeTrue();
        result.ParseResult.Value.Should().Be("value");
        parser.ParseCount.Should().Be(1);
    }

    [Fact]
    public async Task PipelineReturnsNoParserResultWhenNoParserMatches()
    {
        var pipeline = new EmailPipeline<string>(new EmailParserResolver<string>(
        [
            new StubParser("ignored", priority: 1, canParse: false, "wrong")
        ]));

        var result = await pipeline.ProcessAsync(CreateMessage());

        result.HasParser.Should().BeFalse();
        result.ParseResult.IsSuccess.Should().BeFalse();
        result.ParseResult.Errors.Should().ContainSingle(error => error.Code == "NoParser");
    }

    [Fact]
    public void EmailHeaderCollectionIsCaseInsensitive()
    {
        var headers = new EmailHeaderCollection(
        [
            new KeyValuePair<string, string>("Subject", "Monthly statement")
        ]);

        headers["subject"].Should().Be("Monthly statement");
        headers.ContainsKey("SUBJECT").Should().BeTrue();
    }

    private static EmailMessage CreateMessage() =>
        new()
        {
            Id = "message-1",
            ThreadId = "thread-1",
            Subject = "statement"
        };

    private sealed class StubParser : IEmailParser<string>
    {
        private readonly bool _canParse;
        private readonly string _value;

        public StubParser(string name, int priority, bool canParse, string value)
        {
            Name = name;
            Priority = priority;
            _canParse = canParse;
            _value = value;
        }

        public string Name { get; }

        public int Priority { get; }

        public int ParseCount { get; private set; }

        public bool CanParse(EmailMessage message) => _canParse;

        public Task<EmailParseResult<string>> ParseAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default)
        {
            ParseCount++;
            return Task.FromResult(EmailParseResult<string>.Succeeded(_value));
        }
    }
}
