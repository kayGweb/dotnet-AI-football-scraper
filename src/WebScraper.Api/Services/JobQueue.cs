using System.Threading.Channels;

namespace WebScraper.Api.Services;

public class JobQueue : IJobQueue
{
    private readonly Channel<int> _channel;

    public JobQueue()
    {
        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelReader<int> Reader => _channel.Reader;

    public bool TryEnqueue(int jobId)
    {
        return _channel.Writer.TryWrite(jobId);
    }
}
