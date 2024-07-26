using Amazon.S3;
using Amazon.S3.Model;
using TwitchBot.Config;

namespace TwitchBot.AWS
{
    public class AwsClient
    {
        readonly Logger log = new("AWS");
        public bool Enabled = false;
        IAmazonS3 S3;
        public AwsClient(bool enabled = true)
        {
            Enabled = enabled;
            S3 = new AmazonS3Client(AwsConfig.HellbotCredentials, Amazon.RegionEndpoint.USEast1);
            log.Info($"Configured S3 Client for {S3.Config.RegionEndpoint}");
        }

        public bool IsEnabled()
        {
            return Enabled;
        }

        public async Task<bool> ListS3BucketObjects()
        {
            if (!Enabled) { return false; }

            try
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = AwsConfig.HellbotBucket,
                    MaxKeys = 5,
                };

                ListObjectsV2Response response;

                do
                {
                    response = await S3.ListObjectsV2Async(request);

                    response.S3Objects
                        .ForEach(obj => Console.WriteLine($"{obj.Key,-35}{obj.LastModified.ToShortDateString(),10}{obj.Size,10}"));
                    request.ContinuationToken = response.NextContinuationToken;
                }
                while (response.IsTruncated);

                return true;
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                return false;
            }
        }

        public async Task<bool> UploadToS3(string localFilePath, string s3fileName)
        {
            if (!Enabled) { return false; }

            var request = new PutObjectRequest
            {
                BucketName = AwsConfig.HellbotBucket,
                FilePath = localFilePath,
                Key = s3fileName,
            };
            log.Info($"S3 Request: {request.BucketName}");
            var response = await S3.PutObjectAsync(request);
            log.Info($"S3 Response: {response.HttpStatusCode}");
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        public async Task<string> ReadFromS3(string s3fileName)
        {
            if (!Enabled) { return ""; }

            var request = new GetObjectRequest
            {
                BucketName = AwsConfig.HellbotBucket,
                Key = s3fileName,
            };

            using var response = await S3.GetObjectAsync(request);
            using var reader = new StreamReader(response.ResponseStream);
            var text = reader.ReadToEnd();

            return text;
        }
    }
}
