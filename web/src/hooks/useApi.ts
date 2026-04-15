import { useQuery } from '@tanstack/react-query';
import type { PostsResponse, DailySummary, TrendsResponse } from '../types';

const API_BASE = import.meta.env.VITE_API_URL || 'https://api.example.com';

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`API error: ${response.status}`);
  }
  return response.json();
}

export function usePosts(date?: string) {
  return useQuery({
    queryKey: ['posts', date],
    queryFn: () => 
      fetchJson<PostsResponse>(
        date ? `${API_BASE}/posts/${date}` : `${API_BASE}/posts`
      ),
    staleTime: 1000 * 60 * 5, // 5 minutes
  });
}

export function useDailySummary(date: string) {
  return useQuery({
    queryKey: ['summary', date],
    queryFn: () => fetchJson<DailySummary>(`${API_BASE}/summary/${date}`),
    staleTime: 1000 * 60 * 30, // 30 minutes
  });
}

export function useTrends(days: number = 7) {
  return useQuery({
    queryKey: ['trends', days],
    queryFn: () => fetchJson<TrendsResponse>(`${API_BASE}/trends?days=${days}`),
    staleTime: 1000 * 60 * 30, // 30 minutes
  });
}
