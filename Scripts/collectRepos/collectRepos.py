import requests
import time
import math
from datetime import datetime, timedelta

# GitHub API URL for repository search
search_url = 'https://api.github.com/search/repositories'

# Define the start and end date for searches
start_date = datetime(2008, 4, 1)  # GitHub's inception
end_date = datetime(2024, 8, 31)  # Specified end date

# Number of days for each interval
interval_days = 14

# Parameters for the search query
common_params = {
    'q': 'language:c# pushed:>=2023-08-27 archived:false template:false stars:>=50 license:mit license:apache-2.0 license:cc license:0bsd',
    'sort': 'stars',
    'order': 'desc',
    'per_page': 100
}

# Rate limit parameters
rate_limit_wait = 60  # Time to wait between requests in seconds


def search_github_repositories(query_params):
    """Search for repositories on GitHub based on query parameters."""
    all_repositories = []
    page = 1
    while True:
        query_params['page'] = page
        response = requests.get(search_url, params=query_params)
        if response.status_code == 200:
            data = response.json()
            repositories = data.get('items', [])
            if not repositories:
                break
            all_repositories.extend(repositories)
            page += 1
        elif response.status_code == 403 and 'X-RateLimit-Remaining' in response.headers:
            # Handle rate limit exceeded
            remaining = int(response.headers['X-RateLimit-Remaining'])
            if remaining == 0:
                reset_time = int(response.headers['X-RateLimit-Reset'])
                wait_time = math.ceil(abs(reset_time - time.time()))
                print(f"Rate limit exceeded. Waiting for {wait_time} seconds.")
                time.sleep(wait_time)
            else:
                # General error handling
                print(f"Failed to retrieve repositories: {response.status_code}")
                break
        else:
            print(f"Failed to retrieve repositories: {response.status_code}")
            break
    return all_repositories


def save_repository_urls(repositories, filename):
    """Save repository URLs to a text file."""
    with open(filename, 'w') as file:
        for repo in repositories:
            file.write(f"{repo.get('html_url')}\n")


def generate_date_ranges(start_date, end_date, interval_days):
    """Generate a list of date ranges for each interval."""
    ranges = []
    current_start = start_date
    while current_start < end_date:
        current_end = min(current_start + timedelta(days=interval_days), end_date)
        ranges.append((current_start, current_end))
        current_start = current_end
    return ranges


def main():
    all_repositories = []

    # Generate date ranges
    date_ranges = generate_date_ranges(start_date, end_date, interval_days)

    for start, end in date_ranges:
        # Format dates for GitHub API query
        created_after = start.isoformat()
        created_before = end.isoformat()

        # Define search parameters for the current date range
        query_params = common_params.copy()
        query_params['q'] += f' created:{created_after}..{created_before}'

        # Search GitHub repositories within the current date range
        repositories = search_github_repositories(query_params)
        all_repositories.extend(repositories)
        print(f"Found {len(repositories)} repositories from {created_after} to {created_before}")

    # Save repository URLs to a text file
    if all_repositories:
        save_repository_urls(all_repositories, 'repositories_urls.txt')
        print(f"Saved {len(all_repositories)} repository URLs to 'repositories_urls.txt'")
    else:
        print('No repositories found.')


if __name__ == '__main__':
    main()
