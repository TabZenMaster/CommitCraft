using System.Threading.Channels;

namespace CodeReview.Application.Service;

/// <summary>
/// 审核任务后台队列（Channel 实现，无锁线程安全）
/// </summary>
public class ReviewQueueService
{
    private readonly Channel<int> _channel;

    public ReviewQueueService()
    {
        // Bounded: 积压超过100条时丢弃最旧的任务，避免内存爆炸
        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <summary>入队，返回是否成功（队列满时返回 false）</summary>
    public bool Enqueue(int reviewCommitId)
    {
        return _channel.Writer.TryWrite(reviewCommitId);
    }

    /// <summary>异步获取下一个任务（可取消）</summary>
    public async Task<int> DequeueAsync(CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }
}
