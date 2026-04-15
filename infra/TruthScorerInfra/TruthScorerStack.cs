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
        public TruthScorerStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            // Add project tag to all resources
            Amazon.CDK.Tags.Of(this).Add("Project", "TruthScorer");
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
                Description = "Fetches Donald Trump Truth Social posts and stores new posts in DynamoDB.",
                Runtime = LambdaRuntime.DOTNET_8,
                Handler = "ScraperFunction::ScraperFunction.Function::FunctionHandler",
                Code = LambdaCode.FromAsset("../backend/ScraperFunction/bin/Release/net8.0/publish"),
                MemorySize = 512,
                Timeout = Duration.Minutes(2),
                Environment = new Dictionary<string, string>(lambdaEnvironment)
                {
                    { "SCRAPECREATORS_API_KEY_PARAM", "/truthscorer/scrapecreators-api-key" }
                }
            });

            // Analysis Function - calls Bedrock for AI scoring
            var analysisFunction = new LambdaFunction(this, "AnalysisFunction", new LambdaFunctionProps
            {
                FunctionName = "TruthScorer-Analysis",
                Description = "Analyzes Truth Social posts with Bedrock and stores mental and moral scores.",
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
                Description = "Aggregates daily Truth Social activity into summary metrics and scores.",
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
                Description = "Serves the Truth Scorer REST API for posts, summaries, and trends.",
                Runtime = LambdaRuntime.DOTNET_8,
                Handler = "ApiFunction::ApiFunction.Function::FunctionHandler",
                Code = LambdaCode.FromAsset("../backend/ApiFunction/bin/Release/net8.0/publish"),
                MemorySize = 512,
                Timeout = Duration.Seconds(30),
                Environment = lambdaEnvironment
            });

            // Backfill Function - operational tooling for historical ingest/analysis/summary regeneration
            var backfillFunction = new LambdaFunction(this, "BackfillFunction", new LambdaFunctionProps
            {
                FunctionName = "TruthScorer-Backfill",
                Description = "On-demand backfill workflow for historical posts, analyses, and summaries.",
                Runtime = LambdaRuntime.DOTNET_8,
                Handler = "BackfillFunction::BackfillFunction.Function::FunctionHandler",
                Code = LambdaCode.FromAsset("../backend/BackfillFunction/bin/Release/net8.0/publish"),
                MemorySize = 1024,
                Timeout = Duration.Minutes(15),
                Environment = new Dictionary<string, string>(lambdaEnvironment)
                {
                    { "SCRAPECREATORS_API_KEY_PARAM", "/truthscorer/scrapecreators-api-key" }
                }
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
                Actions = new[] { "bedrock:InvokeModel", "bedrock:InvokeModelWithResponseStream" },
                Resources = new[] { "*" }
            }));
            analysisFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = new[] { "aws-marketplace:ViewSubscriptions", "aws-marketplace:Subscribe" },
                Resources = new[] { "*" }
            }));

            // Grant SSM read permission for API key to Scraper function
            scraperFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = new[] { "ssm:GetParameter" },
                Resources = new[] { $"arn:aws:ssm:{Region}:{Account}:parameter/truthscorer/*" }
            }));

            // Backfill function IAM permissions
            postsTable.GrantReadWriteData(backfillFunction);
            analysisTable.GrantReadWriteData(backfillFunction);
            summariesTable.GrantReadWriteData(backfillFunction);
            backfillFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = new[] { "ssm:GetParameter" },
                Resources = new[] { $"arn:aws:ssm:{Region}:{Account}:parameter/truthscorer/*" }
            }));
            backfillFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = new[] { "bedrock:InvokeModel", "bedrock:InvokeModelWithResponseStream" },
                Resources = new[] { "*" }
            }));
            backfillFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = new[] { "aws-marketplace:ViewSubscriptions", "aws-marketplace:Subscribe" },
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
                PublicReadAccess = false,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                RemovalPolicy = RemovalPolicy.DESTROY,
                AutoDeleteObjects = true
            });

            var distribution = new Distribution(this, "WebDistribution", new DistributionProps
            {
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = S3BucketOrigin.WithOriginAccessControl(websiteBucket),
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS
                },
                DefaultRootObject = "index.html",
                ErrorResponses = new[]
                {
                    new ErrorResponse
                    {
                        HttpStatus = 403,
                        ResponseHttpStatus = 200,
                        ResponsePagePath = "/index.html"
                    },
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

            new CfnOutput(this, "WebDistributionId", new CfnOutputProps
            {
                Value = distribution.DistributionId,
                Description = "CloudFront distribution ID"
            });
        }
    }
}
