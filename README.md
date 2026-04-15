# Trump Truth Social Scorer

A platform that scrapes Donald Trump's Truth Social posts, analyzes them with AI for mental and moral scoring, and displays daily summaries with activity patterns.

## Architecture

- **Frontend**: React + Vite + Tailwind CSS
- **Backend**: AWS Lambda (.NET 8 / C#)
- **Database**: DynamoDB
- **AI**: AWS Bedrock (Claude 3 Haiku)
- **Infrastructure**: AWS CDK (C#)
- **CI/CD**: GitHub Actions

## Project Structure

```
├── infra/          # AWS CDK infrastructure (C#)
├── backend/        # Lambda functions (C# .NET 8)
├── web/            # React + Vite + Tailwind frontend
└── .github/        # GitHub Actions workflows
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js 18+
- AWS CLI configured
- AWS CDK CLI (`npm install -g aws-cdk`)
- Expo CLI (`npm install -g expo-cli`)

### Deploy Infrastructure

```bash
cd infra
dotnet build
cdk deploy
```

### Run Web App

```bash
cd web
npm install
npm run dev
```

## Features

- Real-time post scraping (every 15-30 minutes)
- AI-powered mental and moral scoring
- Daily summary dashboard
- Activity pattern tracking (posting times, sleep windows)
- Historical trend analysis
