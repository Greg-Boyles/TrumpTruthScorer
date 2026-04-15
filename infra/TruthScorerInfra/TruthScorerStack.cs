using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3.Deployment;
using Constructs;
using System.Collections.Generic;

// Aliases to resolve ambiguity
using LambdaFunction = Amazon.CDK.AWS.Lambda.Function;
using LambdaFunctionProps = Amazon.CDK.AWS.Lambda.FunctionProps;
using LambdaRuntime = Amazon.CDK.AWS.Lambda.Runtime;
using LambdaCode = Amazon.CDK.AWS.Lambda.Code;
using DynamoAttribute = Amazon.CDK.AWS.DynamoDB.Attribute;
using EventSourceMappingOptions = Amazon.CDK.AWS.Lambda.EventSourceMappingOptions;
using StartingPosition = Amazon.CDK.AWS.Lambda.StartingPosition;
using EventsLambdaTarget = Amazon.CDK.AWS.Events.Targets.LambdaFunction;

namespace TruthScorerInfra
{
    public class TruthScorerStack : Stack
    {
        public TruthScorerStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // ============================================
            // DynamoDB Tables
            // ============================================
            
            // Posts table - stores raw Truth Social posts
            var postsTable = new Table(this, "PostsTable", new TableProps
            {
                TableName = "TruthScorer-Posts",
                PartitionKey = new DynamoAttribute { Name = "postId", Type = AttributeType.STRING },
                SortKey = new DynamoAttribute { Name = "createdAt", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                Stream = StreamViewType.NEW_IMAGE,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // GSI for querying by date
            postsTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
            {
                IndexName = "DateIndex",
                PartitionKey = new DynamoAttribute { Name = "datePartition", Type = AttributeType.STRING },
                SortKey = new DynamoAttribute { Name = "createdAt", Type = AttributeType.STRING },
                ProjectionType = ProjectionType.ALL
            });

            // Analysis table - stores AI analysis results
            var analysisTable = new Table(this, "AnalysisTable", new TableProps
            {
                TableName = "TruthScorer-Analysis",
                PartitionKey = new DynamoAttribute { Name = "postId", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Daily summaries table
            var summariesTable = new Table(this, "SummariesTable", new TableProps
            {
                TableName = "TruthScorer-DailySummaries",
                PartitionKey = new DynamoAttribute { Name = "date", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // ============================================
            // Lambda Functions
            // ============================================

            // Common Lambda props
            var lambdaEnvironment = new Dictionary<string, string>
            {
                { "POSTS_TABLE", postsTable.TableName },
                { "ANALYSIS_TABLE", analysisTable.TableName },
                { "SUMMARIES_TABLE", summariesTable.TableName }
            };

            // Scraper Function - fetches posts from Truth Social
            var scraperFunction = new LambdaFunction(this, "ScraperFunction", new LambdaFunctionProps
            {
                FunctionName = "TruthScorer-Scraper",
                Runtime = LambdaRuntime.DOTNET_8,
                Handler = "ScraperFunction::ScraperFunction.Function::FunctionHandler",
                Code = LambdaCode.FromAsset("../backend/ScraperFunction/bin/Release/net8.0/publish"),
                MemorySize = 512,
                Timeout = Duration.Minutes(2),
                Environment = new Dictionary<string, string>(lambdaEnvironment)
                {
                    { "SCRAPECREATORS_API_KEY", "{{resolve:ssm:/truthscorer/scrapecreators-api-key}}" }
                }
            });

            // Analysis Function - calls Bedrock for AI scoring
            var analysisFunction = new LambdaFunction(this, "AnalysisFunction", new LambdaFunctionProps
            {
                FunctionName = "TruthScorer-Analysis",
                Runtime = LambdaRuntime.DOTNET_8,
                Handler = "AnalysisFunction::AnalysisFunction.Function::FunctionHandler",
                Code = LambdaCode.FromAsset("../backend/AnalysisFunction/bin/Release/net8.0/publish"),
                MemorySize = 512,
                Timeout = Duration.Minutes(2),
                Environment = lambdaEnvironment
            });

            // Daily Summary Function - aggregates daily scores
            var summaryFunction = new LambdaFunction(this, "SummaryFunction", new LambdaFunctionProps
            {
                FunctionName = "TruthScorer-DailySummary",
                Runtime = LambdaRuntime.DOTNET_8,
                Handler = "SummaryFunction::SummaryFunction.Function::FunctionHandler",
                Code = LambdaCode.FromAsset("../backend/SummaryFunction/bin/Release/net8.0/publish"),
                MemorySize = 512,
                Timeout = Duration.Minutes(5),
                Environment = lambdaEnvironment
            });

            // API Function - handles REST API requests
            var apiFunction = new LambdaFunction(this, "ApiFunction", new LambdaFunctionProps
            {
                FunctionName = "TruthScorer-Api",
                Runtime = LambdaRuntime.DOTNET_8,
                Handler = "ApiFunction::ApiFunction.Function::FunctionHandler",
                Code = LambdaCode.FromAsset("../backend/ApiFunction/bin/Release/net8.0/publish"),
                MemorySize = 512,
                Timeout = Duration.Seconds(30),
                Environment = lambdaEnvironment
            });

            // ============================================
            // IAM Permissions
            // ============================================

            // Grant DynamoDB permissions
            postsTable.GrantReadWriteData(scraperFunction);
            postsTable.GrantReadWriteData(analysisFunction);
            postsTable.GrantReadData(summaryFunction);
            postsTable.GrantReadData(apiFunction);

            analysisTable.GrantReadWriteData(analysisFunction);
            analysisTable.GrantReadData(summaryFunction);
            analysisTable.GrantReadData(apiFunction);

            summariesTable.GrantReadWriteData(summaryFunction);
            summariesTable.GrantReadData(apiFunction);

            // Grant Bedrock permissions to Analysis function
            analysisFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = new[] { "bedrock:InvokeModel" },
                Resources = new[] { "*" }
            }));

            // ============================================
            // EventBridge Schedules
            // ============================================

            // Scraper runs every 30 minutes
            var scraperRule = new Rule(this, "ScraperSchedule", new RuleProps
            {
                RuleName = "TruthScorer-ScraperSchedule",
                Schedule = Schedule.Rate(Duration.Minutes(30))
            });
            scraperRule.AddTarget(new EventsLambdaTarget(scraperFunction));

            // Daily summary runs at midnight UTC
            var summaryRule = new Rule(this, "SummarySchedule", new RuleProps
            {
                RuleName = "TruthScorer-DailySummarySchedule",
                Schedule = Schedule.Cron(new CronOptions
                {
                    Minute = "0",
                    Hour = "0"
                })
            });
            summaryRule.AddTarget(new EventsLambdaTarget(summaryFunction));

            // ============================================
            // DynamoDB Stream Trigger for Analysis
            // ============================================

            analysisFunction.AddEventSourceMapping("PostsStreamTrigger", new EventSourceMappingOptions
            {
                EventSourceArn = postsTable.TableStreamArn,
                StartingPosition = StartingPosition.LATEST,
                BatchSize = 10
            });

            // Grant stream read permissions
            postsTable.GrantStreamRead(analysisFunction);

            // ============================================
            // API Gateway
            // ============================================

            var api = new RestApi(this, "TruthScorerApi", new RestApiProps
            {
                RestApiName = "TruthScorer API",
                Description = "API for Trump Truth Social Scorer",
                DefaultCorsPreflightOptions = new CorsOptions
                {
                    AllowOrigins = Cors.ALL_ORIGINS,
                    AllowMethods = Cors.ALL_METHODS,
                    AllowHeaders = new[] { "Content-Type", "Authorization" }
                }
            });

            var apiIntegration = new LambdaIntegration(apiFunction);

            // GET /posts
            var postsResource = api.Root.AddResource("posts");
            postsResource.AddMethod("GET", apiIntegration);

            // GET /posts/{date}
            var postsByDateResource = postsResource.AddResource("{date}");
            postsByDateResource.AddMethod("GET", apiIntegration);

            // GET /summary/{date}
            var summaryResource = api.Root.AddResource("summary");
            var summaryByDateResource = summaryResource.AddResource("{date}");
            summaryByDateResource.AddMethod("GET", apiIntegration);

            // GET /trends
            var trendsResource = api.Root.AddResource("trends");
            trendsResource.AddMethod("GET", apiIntegration);

            // ============================================
            // S3 + CloudFront for Web Hosting
            // ============================================

            var websiteBucket = new Bucket(this, "WebsiteBucket", new BucketProps
            {
                BucketName = $"truthscorer-web-{Account}",
                WebsiteIndexDocument = "index.html",
                WebsiteErrorDocument = "index.html",
                PublicReadAccess = false,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                RemovalPolicy = RemovalPolicy.DESTROY,
                AutoDeleteObjects = true
            });

            var originAccessIdentity = new OriginAccessIdentity(this, "OAI");
            websiteBucket.GrantRead(originAccessIdentity);

            var distribution = new Distribution(this, "WebDistribution", new DistributionProps
            {
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = new S3Origin(websiteBucket, new S3OriginProps
                    {
                        OriginAccessIdentity = originAccessIdentity
                    }),
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS
                },
                DefaultRootObject = "index.html",
                ErrorResponses = new[]
                {
                    new ErrorResponse
                    {
                        HttpStatus = 404,
                        ResponseHttpStatus = 200,
                        ResponsePagePath = "/index.html"
                    }
                }
            });

            // ============================================
            // Outputs
            // ============================================

            new CfnOutput(this, "ApiEndpoint", new CfnOutputProps
            {
                Value = api.Url,
                Description = "API Gateway endpoint URL"
            });

            new CfnOutput(this, "WebsiteUrl", new CfnOutputProps
            {
                Value = $"https://{distribution.DistributionDomainName}",
                Description = "CloudFront website URL"
            });

            new CfnOutput(this, "WebsiteBucketName", new CfnOutputProps
            {
                Value = websiteBucket.BucketName,
                Description = "S3 bucket for website hosting"
            });
        }
    }
}
