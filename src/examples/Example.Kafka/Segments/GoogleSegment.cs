using Pipelinez.Core.Segment;

namespace Example.Kafka.Segments;

public class GoogleSegment : PipelineSegment<KafkaRecordJSON>
{
    public override async Task<KafkaRecordJSON> ExecuteAsync(KafkaRecordJSON arg)
    {
        string url = "http://www.google.com/";
        using(var client = new HttpClient())
        {
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Clear();

            var response = await client.GetAsync(url);

            arg.RecordValue = (await response.Content.ReadAsStringAsync()).Substring(0,500) + "...";
        }

        return arg;
    }
}