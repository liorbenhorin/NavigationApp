import os
from PIL import Image

Image.MAX_IMAGE_PIXELS = 3000000000

root = os.path.dirname(__file__)

# Load the large image
large_image_path = os.path.join(root, "arieal_all.png")  # Replace with the path to your large image
large_image = Image.open(large_image_path)
width, height = large_image.size

# Define the square size
square_size = 4096

# Calculate the number of squares
num_horizontal_squares = (width + square_size - 1) // square_size
num_vertical_squares = (height + square_size - 1) // square_size


# Generate the square images
for i in range(num_horizontal_squares):
    for j in range(num_vertical_squares):
        left = i * square_size
        upper = j * square_size
        right = min(left + square_size, width)
        lower = min(upper + square_size, height)

        # Create a new image with white background
        square_image = Image.new("RGB", (square_size, square_size), (255, 255, 255))

        # Paste the part of the large image into the square_image
        square_image.paste(large_image.crop((left, upper, right, lower)))

        # Save the square image to a file
        square_image_path = f"square_{i}_{j}.jpg"
        square_image.save(os.path.join(root, square_image_path), format='JPEG', quality=90)
