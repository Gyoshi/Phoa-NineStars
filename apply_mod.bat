echo off

rem copy "bin\Debug\net3.5\NineStars.dll" "C:\Program Files (x86)\Steam\steamapps\common\Phoenotopia Awakening\Mods\NineStars\"
rem copy Info.json "C:\Program Files (x86)\Steam\steamapps\common\Phoenotopia Awakening\Mods\NineStars\"
rem copy assets\StreamingAssets\ModifiedLevels "C:\Program Files (x86)\Steam\steamapps\common\Phoenotopia Awakening\Mods\NineStars\ModifiedLevels\"

powershell -Command "Expand-Archive -Force -Path PhoA-NineStars.zip -DestinationPath ""C:\Program Files (x86)\Steam\steamapps\common\Phoenotopia Awakening\Mods\NineStars\"""
