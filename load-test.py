import requests
import time
import random
import concurrent.futures
import json
import argparse

# Configuration
DEFAULT_BASE_URL = "http://localhost:38939"  # Update with your Basket API endpoint
NUM_USERS = 10
REQUESTS_PER_USER = 20
CONCURRENCY = 5  # Number of concurrent users

# Sample user data (will be masked by our processor)
def generate_users(count):
    return [
        {
            "id": f"user{i}",
            "email": f"user{i}@example.com",
            "cc": f"4111-1111-1111-111{i % 10}"
        } 
        for i in range(count)
    ]

def get_basket(base_url, user):
    """Simulate getting a user's basket"""
    try:
        response = requests.get(
            f"{base_url}/api/v1/basket/{user['id']}",
            headers={
                "X-UserId": user["id"],
                "X-UserEmail": user["email"]  # This will be masked
            }
        )
        return response.status_code
    except Exception as e:
        print(f"Error in get_basket: {e}")
        return 500

def update_basket(base_url, user):
    """Simulate updating a user's basket"""
    try:
        basket = {
            "buyerId": user["id"],
            "items": [
                {
                    "productId": str(random.randint(1, 100)),
                    "productName": f"Product {random.randint(1, 100)}",
                    "unitPrice": random.uniform(5.0, 100.0),
                    "quantity": random.randint(1, 5),
                    "pictureUrl": "https://example.com/product.jpg"
                }
            ]
        }
        
        response = requests.post(
            f"{base_url}/api/v1/basket",
            json=basket,
            headers={
                "X-UserId": user["id"],
                "X-UserEmail": user["email"],  # This will be masked
                "X-PaymentInfo": user["cc"],   # This will be masked
                "Content-Type": "application/json"
            }
        )
        return response.status_code
    except Exception as e:
        print(f"Error in update_basket: {e}")
        return 500

def delete_basket(base_url, user):
    """Simulate deleting a user's basket"""
    try:
        response = requests.delete(
            f"{base_url}/api/v1/basket/{user['id']}",
            headers={
                "X-UserId": user["id"]
            }
        )
        return response.status_code
    except Exception as e:
        print(f"Error in delete_basket: {e}")
        return 500

def run_user_scenario(base_url, user):
    """Run a full user scenario multiple times"""
    results = []
    
    for _ in range(REQUESTS_PER_USER):
        start_time = time.time()
        
        # Get the basket
        get_status = get_basket(base_url, user)
        
        # Update the basket
        update_status = update_basket(base_url, user)
        
        # Delete basket (sometimes)
        delete_status = delete_basket(base_url, user) if random.random() > 0.7 else None
        
        elapsed = time.time() - start_time
        
        results.append({
            "user": user["id"],
            "get_status": get_status,
            "update_status": update_status,
            "delete_status": delete_status,
            "elapsed_seconds": elapsed
        })
        
        # Small delay between requests
        time.sleep(random.uniform(0.1, 0.5))
    
    return results

def main():
    parser = argparse.ArgumentParser(description="Load test for eShop Basket API")
    parser.add_argument("--url", default=DEFAULT_BASE_URL, help=f"Base URL (default: {DEFAULT_BASE_URL})")
    parser.add_argument("--users", type=int, default=NUM_USERS, help=f"Number of users (default: {NUM_USERS})")
    parser.add_argument("--requests", type=int, default=REQUESTS_PER_USER, help=f"Requests per user (default: {REQUESTS_PER_USER})")
    parser.add_argument("--concurrency", type=int, default=CONCURRENCY, help=f"Concurrent users (default: {CONCURRENCY})")
    
    args = parser.parse_args()
    
    users = generate_users(args.users)
    all_results = []
    
    print(f"Starting load test with {args.users} users, {args.requests} requests each")
    print(f"Base URL: {args.url}")
    
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.concurrency) as executor:
        future_to_user = {
            executor.submit(run_user_scenario, args.url, user): user for user in users
        }
        
        for future in concurrent.futures.as_completed(future_to_user):
            user = future_to_user[future]
            try:
                results = future.result()
                all_results.extend(results)
                print(f"User {user['id']} completed {len(results)} requests")
            except Exception as exc:
                print(f"User {user['id']} generated an exception: {exc}")
    
    # Summarize results
    total_requests = len(all_results)
    avg_time = sum(r["elapsed_seconds"] for r in all_results) / total_requests if total_requests else 0
    get_success = sum(1 for r in all_results if r["get_status"] == 200)
    update_success = sum(1 for r in all_results if r["update_status"] == 200)
    delete_success = sum(1 for r in all_results if r["delete_status"] == 200 or r["delete_status"] is None)
    
    print(f"\nLoad Test Complete:")
    print(f"Total Requests: {total_requests}")
    print(f"Avg Response Time: {avg_time:.3f} seconds")
    print(f"Get Success: {get_success}/{total_requests} ({get_success/total_requests*100:.1f}%)")
    print(f"Update Success: {update_success}/{total_requests} ({update_success/total_requests*100:.1f}%)")
    print(f"Delete Success: {delete_success}/{total_requests} ({delete_success/total_requests*100:.1f}%)")

if __name__ == "__main__":
    main()