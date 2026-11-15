# Garage wheels

Customize Car has Body and Wheels tabs. Body colours do not recolour wheels.
The model's embedded wheels are the default. NFM wheels retain the model's wheel
positions, dimensions and steering flags, and expose a rim colour picker.

Custom wheel authors can declare up to four independent paint slots in RAD text:

```text
1stWheelColor(120,120,120)
2ndWheelColor(200,80,40)
```

`3rdWheelColor` and `4thWheelColor` work the same way. Matching `c(r,g,b)` polygons
inside custom wheel meshes become editable. These declarations work with all
supported embedded wheel formats. Body `1stColor` through `4thColor` declarations
do not opt wheel polygons into painting. Re-export/repack the model after adding
wheel colour declarations so the compiled wheel polygons retain their slots.

Wheel choices and paint are saved per car in `data/cfg/car-wheels.json` and sent
per player in multiplayer. User paint channels retain the minimum value of 15.
The wheel fields require protocol v4 on the client and servers.

Keyboard: Tab switches Body/Wheels; Up/Down moves between tabs, wheel choices,
paint slots and Reset. Left/Right selects a wheel style or paint control;
Shift+Left/Right adjusts paint. Enter activates Reset; Escape returns.
