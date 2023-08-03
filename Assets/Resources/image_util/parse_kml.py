import xmltodict
import json

def kml_to_json(kml_file):
    with open(kml_file, 'r', encoding='utf-8') as f:
        kml_data = f.read()

    # Parse the KML data into a dictionary using xmltodict
    kml_dict = xmltodict.parse(kml_data)

    # Extract the relevant information and create a list of waypoints
    waypoints = []
    placemarks = kml_dict['kml']['Document']['Folder']['Placemark']
    for placemark in placemarks:
        name = placemark['name']
        coordinates = placemark['Point']['coordinates']
        lon, lat, alt = map(float, coordinates.split(','))  # Convert to float values

        waypoints.append({'name': name, 'coord': (lon, lat)})

    # Create the final JSON structure
    json_data = {'waypoints': waypoints}

    return json_data

if __name__ == "__main__":
    kml_file_path = r"C:\dev\personal\NavigationApp\Assets\Resources\user-waypoints.kml"
    json_data = kml_to_json(kml_file_path)

    with open(r"C:\dev\personal\NavigationApp\Assets\Resources\user-waypoints.json", "w") as json_file:
        json.dump(json_data, json_file, indent=2)
