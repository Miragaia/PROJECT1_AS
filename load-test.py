import random
from locust import HttpUser, task, between

class CatalogUser(HttpUser):
    wait_time = between(1, 3)
    host = "http://localhost:5222"

    @task
    def view_random_catalog_item(self):
        item_id = random.randint(1, 100)
        url = f"/api/catalog/items/{item_id}?api-version=1.0"
        res = self.client.get(url, name="/api/catalog/items/[id]", verify=False)

        if res.status_code == 200:
            print(f"Fetched item {item_id} successfully.")
        else:
            print(f"Failed to fetch item {item_id}, Response: {res.status_code}")
