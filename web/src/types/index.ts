export interface Post {
  postId: string;
  createdAt: string;
  datePartition: string;
  content: string;
  reblogsCount: number;
  favouritesCount: number;
  repliesCount: number;
  url: string;
  mediaUrls: string[];
  isRetruth: boolean;
}

export interface Analysis {
  postId: string;
  mentalScore: number;
  moralScore: number;
  emotionalState: string;
  keyThemes: string[];
  summary: string;
  analyzedAt: string;
}

export interface PostWithAnalysis {
  post: Post;
  analysis: Analysis | null;
}

export interface DailySummary {
  date: string;
  totalPosts: number;
  avgMentalScore: number;
  avgMoralScore: number;
  overallScore: number;
  topThemes: string[];
  summaryText: string;
  postingHours: number[];
  quietHoursStart: number | null;
  quietHoursEnd: number | null;
  createdAt: string;
}

export interface TrendData {
  date: string;
  avgMentalScore: number;
  avgMoralScore: number;
  postCount: number;
}

export interface PostsResponse {
  posts: PostWithAnalysis[];
  date: string;
}

export interface TrendsResponse {
  trends: TrendData[];
}
