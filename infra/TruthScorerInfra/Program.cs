using Amazon.CDK;
using TruthScorerInfra;

var app = new App();

new TruthScorerStack(app, "TruthScorerStack", new StackProps
{
    Env = new Amazon.CDK.Environment
    {
        Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
        Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? "us-east-1"
    }
});

app.Synth();
