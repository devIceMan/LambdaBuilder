#r "packages/FAKE.4.4.5/tools/FakeLib.dll"

open Fake
open Fake.Testing.XUnit2

// Каталоги
let buildDir  = "./.build/"
let testDir   = "./.test/"
let deployDir = "./.deploy/"

let foldersToCleanup =
    !! "**/bin"
    ++ "**/obj"
    ++ buildDir
    ++ testDir
    ++ deployDir

// Для приложения собираем все проекты кроме тестов
let appReferences  = 
    !! "**/*.csproj" 
    -- "**/*.Tests.csproj"

// Для тестов собираем только тестовые проекта
let testReferences = !! "**/*.Tests.csproj"

// Очистка каталогов 
Target "Clean" (fun _ ->     
    CleanDirs foldersToCleanup
)

// Сборка основной либы
Target "Rebuild" (fun _ ->        
    MSBuildRelease buildDir "Rebuild" appReferences
        |> Log "AppBuild-Output: "
)

// Сборка основной либы
Target "RebuildDebug" (fun _ ->        
    MSBuildDebug buildDir "Rebuild" appReferences
        |> Log "AppBuild-Output: "
)

// Запуск тестов
Target "Test" (fun _ ->
    MSBuildDebug testDir "Rebuild" testReferences
        |> Log "TestBuild-Output: ";

    !! (testDir + "/*.Tests.dll")
        |> xUnit2 (fun p -> 
            {p with
                ShadowCopy = false;
                Parallel = Assemblies;
                HtmlOutputPath = Some (testDir @@ "xunit.html");
                XmlOutputPath = Some (testDir @@ "xunit.xml");
            })
)

// Пустая цель по-умоланию
Target "Default" (fun _ -> ())

// Порядок сборки
"Clean"
    ==> "RebuildDebug"    
    ==> "Test"
    ==> "Default"

// По-умолчанию собираем все и тестируем
RunTargetOrDefault "Default"