$PhigrosPath = (adb shell pm path com.PigeonGames.Phigros).Substring(8)
$RawObbPaths = (adb shell ls -1 /storage/emulated/0/Android/obb/com.PigeonGames.Phigros).Split("`n")
$ObbPath = "/storage/emulated/0/Android/obb/com.PigeonGames.Phigros/" + $RawObbPaths[0]

echo $PhigrosPath
echo $ObbPath

adb pull $PhigrosPath .\TestData\base.apk
adb pull $ObbPath .\TestData\obb.obb

if ($RawObbPaths.Count -gt 1) 
{
	echo "Multiple OBB files found, fetching second as aux obb"
	$AuxObbPath = "/storage/emulated/0/Android/obb/com.PigeonGames.Phigros/" + $RawObbPaths[1]
	echo $AuxObbPath
	adb pull $AuxObbPath .\TestData\auxObb.obb
}

#New-Item -Path ".\TestData\obb.obb" -ItemType SymbolicLink -Value ".\TestData\_obb.obb" -Force
