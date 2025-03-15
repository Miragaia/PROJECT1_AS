from locust import HttpUser, TaskSet, task, between

class UserBehavior(TaskSet):
    @task(1)
    def load_homepage(self):
        res = self.client.get("/", verify=False)
        assert res.status_code == 200, "Failed to load homepage"

    @task(2)
    def login(self):
        url = "/Account/Login?ReturnUrl=%2Fconnect%2Fauthorize%2Fcallback%3Frequest_uri%3Durn%253Aietf%253Aparams%253Aoauth%253Arequest_uri%253A66D714483C41C7BE9D813CD505C994811D5AAE2365CAF861DAEE5DDED6D96C68%26client_id%3Dwebapp"
        headers = { "Content-Type": "application/x-www-form-urlencoded" }
        res = self.client.post(url, headers=headers, verify=False)
        assert res.status_code == 200, "Failed to log in"

class WebsiteUser(HttpUser):
    tasks = [UserBehavior]
    wait_time = between(1, 3)  # Simulate user wait time between requests
    host = "https://localhost:5243"  # Replace with your actual base URL
