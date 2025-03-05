import requests
import time
import random
import concurrent.futures

# Configuration
PORT = 45135  # Update to match the actual port your API is running on
BASE_URL = f"http://localhost:{PORT}"
NUM_USERS = 5
REQUESTS_PER_USER = 10
CONCURRENCY = 3

# Sample user data
def generate_users(count):
    return [
        {
            "id": f"user{i}",
            "email": f"user{i}@example.com",
            "cc": f"4111-1111-1111-111{i % 10}"
        } 
        for i in range(count)
    ]

def send_request(url, headers=None, json_data=None, method='GET'):
    """Send a request and return the status code and time taken"""
    start_time = time.time()
    try:
        if method == 'GET':
            response = requests.get(url, headers=headers)
        elif method == 'POST':
            response = requests.post(url, headers=headers, json=json_data)
        elif method == 'DELETE':
            response = requests.delete(url, headers=headers)
        else:
            return 0, 0
            
        return response.status_code, time.time() - start_time
    except Exception as e:
        print(f"Error: {e}")
        return 500, time.time() - start_time

def run_user_scenario(user):
    """Run a simple user scenario"""
    results = []
    user_id = user['id']
    
    for _ in range(REQUESTS_PER_USER):
        # Try different endpoints with sensitive data
        endpoints = [
            # GraphQL endpoint with query param
            (f"{BASE_URL}/graphql?query={{basket(userId:\"{user_id}\"){{items{{productId,quantity}}}}}}",
             {'X-User-Email': user['email']},
             None,
             'GET'),
            
            # REST basket endpoint
            (f"{BASE_URL}/api/v1/basket/{user_id}",
             {'X-User': user_id, 'X-Email': user['email']},
             None,
             'GET'),
            
            # Update basket with credit card info
            (f"{BASE_URL}/api/v1/basket",
             {'Content-Type': 'application/json', 'X-Payment': user['cc']},
             {"buyerId": user_id, "items": [{"productId": str(random.randint(1, 100)), "quantity": random.randint(1, 5)}]},
             'POST')
        ]
        
        endpoint, headers, data, method = random.choice(endpoints)
        status, time_taken = send_request(endpoint, headers, data, method)
        
        results.append({
            'user': user_id,
            'endpoint': endpoint,
            'status': status,
            'time': time_taken
        })
        
        time.sleep(random.uniform(0.1, 0.5))
    
    return results

def main():
    users = generate_users(NUM_USERS)
    all_results = []
    
    print(f"Starting load test with {NUM_USERS} users, {REQUESTS_PER_USER} requests each")
    print(f"Using base URL: {BASE_URL}")
    
    with concurrent.futures.ThreadPoolExecutor(max_workers=CONCURRENCY) as executor:
        future_to_user = {executor.submit(run_user_scenario, user): user for user in users}
        
        for future in concurrent.futures.as_completed(future_to_user):
            user = future_to_user[future]
            try:
                results = future.result()
                all_results.extend(results)
                print(f"User {user['id']} completed {len(results)} requests")
            except Exception as e:
                print(f"Error with user {user['id']}: {e}")
    
    # Print summary
    total = len(all_results)
    successes = sum(1 for r in all_results if r['status'] < 400)
    avg_time = sum(r['time'] for r in all_results) / total if total > 0 else 0
    
    print(f"\nResults: {successes}/{total} successful requests ({successes/total*100:.1f}%)")
    print(f"Average response time: {avg_time*1000:.2f}ms")

if __name__ == "__main__":
    main()