$PhigrosPath = (adb shell pm path com.PigeonGames.Phigros).Substring(8)
$ObbPath = "/storage/emulated/0/Android/obb/com.PigeonGames.Phigros/" + (adb shell ls -1 /storage/emulated/0/Android/obb/com.PigeonGames.Phigros).Split("`n")[0]
echo $PhigrosPath
echo $ObbPath

adb pull $PhigrosPath .\TestData\base.apk
adb pull $ObbPath .\TestData\obb.obb

#New-Item -Path ".\TestData\obb.obb" -ItemType SymbolicLink -Value ".\TestData\_obb.obb" -Force
