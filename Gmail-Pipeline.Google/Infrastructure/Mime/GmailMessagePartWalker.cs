using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Infrastructure.Mime;

public static class GmailMessagePartWalker
{
    public static IEnumerable<(MessagePart Part, string Path)> Walk(MessagePart root)
    {
        foreach (var item in Walk(root, "0"))
        {
            yield return item;
        }
    }

    private static IEnumerable<(MessagePart Part, string Path)> Walk(MessagePart part, string path)
    {
        yield return (part, path);

        if (part.Parts is null)
        {
            yield break;
        }

        for (var index = 0; index < part.Parts.Count; index++)
        {
            foreach (var child in Walk(part.Parts[index], $"{path}.{index}"))
            {
                yield return child;
            }
        }
    }
}
