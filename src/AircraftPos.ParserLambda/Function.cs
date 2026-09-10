using AircraftPos.Application.Interfaces;
using AircraftPos.Application.Services;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AircraftPos.ParserLambda
{
    public class Function
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IPosReportParser _posReportParser;
        public Function()
        {
            _s3Client = new AmazonS3Client();
            _posReportParser = new PosReportParser();
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input">The event for the Lambda function handler to process.</param>
        /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
        /// <returns></returns>
        public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
        {
            foreach(var record in s3Event.Records)
            {
                var bucketName = record.S3.Bucket.Name;
                var objectKey = record.S3.Object.Key;

                context.Logger.LogInformation($"S3 object created: {bucketName}/{objectKey}");

                var response = await _s3Client.GetObjectAsync(
                    new GetObjectRequest
                    {
                        BucketName = bucketName,
                        Key = objectKey
                    });

                using var reader = new StreamReader(response.ResponseStream);
                var rawMessage = await reader.ReadToEndAsync();
                context.Logger.LogInformation($"Raw POS message: {rawMessage}");

                var posReport = _posReportParser.Parse(rawMessage, DateTime.UtcNow);
                context.Logger.LogInformation($"Parsed flight: {posReport.FlightNumber}");

                // Convert PosReport to JSON
                var json = JsonSerializer.Serialize(posReport);

                // Create attachment S3 key
                var attachmentKey =
                    $"attachment/{posReport.FlightId}-{posReport.Timestamp:yyyy-MM-ddTHH:mm:ssZ}.json";

                // Save JSON to S3
                await _s3Client.PutObjectAsync(
                    new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = attachmentKey,
                        ContentBody = json,
                        ContentType = "application/json"
                    });
                context.Logger.LogInformation($"Attachment saved to S3: {bucketName}/{attachmentKey}");
            }
        }
    }
}
