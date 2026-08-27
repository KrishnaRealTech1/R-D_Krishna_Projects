G-Cam AI model folder - v1.0.5

PERSON / VEHICLE
----------------
Preferred optional model:
  person_vehicle.onnx
  coco80.txt

If person_vehicle.onnx is missing, the app automatically falls back to MobileNet-SSD.
build-win-x64.ps1 downloads these fallback files automatically:
  MobileNetSSD_deploy.prototxt
  MobileNetSSD_deploy.caffemodel

The fallback detects Person and common vehicle classes (Car, Bus, Motorbike, Bicycle).

LICENSE PLATE
-------------
Provide:
  license_plate.onnx
  license_plate.txt

GARBAGE
-------
Provide:
  garbage.onnx
  garbage.txt

The source/reference attachments did not include license-plate weights or the garbage ONNX binary.
