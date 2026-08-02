using System.Text;
using FluentAssertions;
using GmailPipeline.Core.Models;
using GmailPipeline.Google.Mime;
using MimeKit;

namespace GmailPipeline.Google.Test.Unit.Mime;

public sealed class GmailMimeParserTests
{
    [Fact]
    public void ParseExtractsMultipartAlternativeBodiesAndAttachmentBytes()
    {
        var message = LoadMime("""
            MIME-Version: 1.0
            Subject: Statement
            Content-Type: multipart/mixed; boundary=mix

            --mix
            Content-Type: multipart/alternative; boundary=alt

            --alt
            Content-Type: text/plain; charset=iso-8859-1
            Content-Transfer-Encoding: quoted-printable

            Caf=E9 statement
            --alt
            Content-Type: text/html; charset=utf-8

            <b>Cafe statement</b>
            --alt--
            --mix
            Content-Type: application/zip; name="statement.zip"
            Content-Disposition: attachment; filename="statement.zip"
            Content-Transfer-Encoding: base64

            UEsDBA==
            --mix--
            """);

        var result = new GmailMimeParser().Parse(message);

        result.TextBody.Should().Be("Caf\u00e9 statement");
        result.HtmlBody.Should().Be("<b>Cafe statement</b>");
        result.Attachments.Should().ContainSingle();
        result.Attachments[0].Id.Should().Be("0.1");
        result.Attachments[0].ExternalContentId.Should().BeNull();
        result.Attachments[0].EmbeddedContent.ToArray().Should().Equal(Convert.FromBase64String("UEsDBA=="));
        result.Attachments[0].FileName.Should().Be("statement.zip");
        result.Attachments[0].Disposition.Should().Be(EmailAttachmentDisposition.Attachment);
    }

    [Fact]
    public void ParseTreatsRelatedContentIdPartAsInlineAttachment()
    {
        var message = LoadMime("""
            MIME-Version: 1.0
            Content-Type: multipart/related; boundary=related

            --related
            Content-Type: text/html; charset=utf-8

            <img src="cid:logo@example">
            --related
            Content-Type: image/png
            Content-Disposition: inline; filename="logo.png"
            Content-ID: <logo@example>
            Content-Transfer-Encoding: base64

            iVBORw0KGgo=
            --related--
            """);

        var attachment = new GmailMimeParser().Parse(message).Attachments.Should().ContainSingle().Subject;

        attachment.IsInline.Should().BeTrue();
        attachment.ContentId.Should().Be("logo@example");
        attachment.FileName.Should().Be("logo.png");
        attachment.EmbeddedContent.ToArray().Should().Equal(Convert.FromBase64String("iVBORw0KGgo="));
    }

    [Fact]
    public void ParseKeepsAttachedRfc822MessageAsAttachment()
    {
        var message = LoadMime("""
            MIME-Version: 1.0
            Content-Type: multipart/mixed; boundary=mix

            --mix
            Content-Type: text/plain; charset=utf-8

            See forwarded message.
            --mix
            Content-Type: message/rfc822
            Content-Disposition: attachment; filename="forwarded.eml"

            From: nested@example.test
            To: me@example.test
            Subject: Nested

            Nested body
            --mix--
            """);

        var result = new GmailMimeParser().Parse(message);

        result.TextBody.Should().Be("See forwarded message.");
        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.MediaType.Should().Be("message/rfc822");
        attachment.FileName.Should().Be("forwarded.eml");
        Encoding.UTF8.GetString(attachment.EmbeddedContent.ToArray()).Should().Contain("Subject: Nested");
    }

    [Fact]
    public void Base64UrlDecoderHandlesMissingPaddingAndUrlAlphabet()
    {
        Base64UrlDecoder.DecodeUtf8("SGVsbG8td29ybGQ").Should().Be("Hello-world");
    }

    private static MimeMessage LoadMime(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.ReplaceLineEndings("\r\n"));
        using var stream = new MemoryStream(bytes);
        return MimeMessage.Load(stream);
    }
}
