Material AI Standalone - Run Guide
==================================

Files in this package:
1. material_ai_standalone.py
   Main Windows desktop app.

2. train_material_ai_model.py
   AI model training/export script. Use this after collecting material images.

3. requirements_app.txt
   Dependencies for running the app.

4. requirements_ai.txt
   Dependencies for training the AI model.

How to run the app:
-------------------
1. Open PyCharm.
2. Create a new Python project.
3. Copy material_ai_standalone.py into the project.
4. Install app dependencies:

   pip install -r requirements_app.txt

5. Run:

   python material_ai_standalone.py

6. The app creates config.ini automatically here:

   C:\Users\<YourUser>\Documents\MaterialAI\config.ini

7. Click "Edit Config" in the app, or open config.ini in Notepad.

Important config sections:
--------------------------
[InputSerial]
Sensor input COM port and baudrate.

[OutputSerial]
Output COM port and baudrate.

[Trigger]
capture_delay_seconds = 5

[Storage]
files_directory = where captured files are stored on the PC.

[SFTP]
Server address, username, password, remote path.

[AI]
Keep enabled = no until you train a model.

AI model training:
------------------
A ready accurate AI model cannot be shared without your real site images.
Use your own camera images and folder structure:

MaterialAI_Dataset\
    Raw Material\
    6mm Jalli\
    12mm Jalli\
    20mm Jalli\
    35mm Jalli\
    M-Sand\
    P-Sand\
    Wet Mix\
    Empty\
    Other\

Install AI dependencies:

   pip install -r requirements_ai.txt

Train:

   python train_material_ai_model.py --dataset "C:\MaterialAI_Dataset" --output "C:\Users\<YourUser>\MaterialAI\model" --epochs 25 --fine-tune-epochs 5

After training, edit config.ini:

[AI]
enabled = yes
input_normalization = raw_0_255

Then restart the app.
