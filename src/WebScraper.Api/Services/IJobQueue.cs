using System.Threading.Channels;

namespace WebScraper.Api.Services;

public interface IJobQueue
{
    bool TryEnqueue(int jobId);
    ChannelReader<int> Reader { get; }
}
