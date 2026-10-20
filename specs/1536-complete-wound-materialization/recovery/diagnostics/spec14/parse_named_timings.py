"""Reconstruct the single T296 measurement from its saved raw child output.

This offline reader never starts the game or changes its fixtures. Use --check
to verify the existing deterministic JSON/Markdown reports without writing them.
"""

from pathlib import Path
import argparse
import hashlib
import json
import math
import re
import statistics
import xml.etree.ElementTree as ET
import zipfile


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def records(raw):
    result = []
    for line in raw.splitlines():
        if not line.startswith("{"):
            continue
        try:
            value = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict):
            result.append(value)
    return result


def build_report(repo):
    evidence = repo / "specs/1536-complete-wound-materialization/recovery/crash5"
    stem = "T296-profile-cut0"
    outcome = load(evidence / (stem + "-outcome.json"))
    archive_path = evidence / (stem + "-raw.zip")
    assert digest(archive_path.read_bytes()) == outcome["ArchiveSHA256"]
    archive_index = []
    with zipfile.ZipFile(archive_path) as archive:
        assert archive.testzip() is None, "Bad ZIP CRC"
        members = archive.namelist()
        assert len(members) == len(set(members)), "Duplicate archive members"
        assert all(not x.startswith(("/", "\\")) and ".." not in x.split("/") for x in members)
        for member in archive.infolist():
            if not member.is_dir():
                raw = archive.read(member)
                archive_index.append({"Path": member.filename, "Bytes": len(raw), "SHA256": digest(raw)})
        assert json.loads(archive.read("summary.json")) == outcome["Summary"]
        assert json.loads(archive.read("T296-seed-binding.json")) == outcome["SeedBinding"]
        prep = json.loads(archive.read("T295-prep.json"))
        assert prep["Binding"]["CompiledSource"] == outcome["CompiledSource"]
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        results = []
        for member in members:
            if member.endswith(".trx"):
                results.extend(ET.fromstring(archive.read(member)).findall(".//t:Results/t:UnitTestResult", ns))
        assert len(results) == 1
        assert results[0].get("outcome") == outcome["Outcome"]
        assert results[0].get("duration") == outcome["TestDuration"]
        outer = records(results[0].findtext("t:Output/t:StdOut", "", ns))
        children = [x["Observation"] for x in outer if x.get("Boundary") == "iobs_actual_native_join"]
    assert len(children) == len(outcome["Children"])
    stream_bindings = []
    for index, child in enumerate(children):
        assert child["Id"] == outcome["Children"][index]["Id"]
        for stream in ("stdout", "stderr"):
            path = evidence / f"{stem}-child{index}.{stream}"
            raw = path.read_bytes()
            assert raw == (child.get("Raw" + stream.title()) or "").encode("utf-8")
            stream_bindings.append({"Path": path.name, "PID": child["Id"], "Bytes": len(raw), "SHA256": digest(raw)})
    resumers = [x for x in children if "engine-joint-interface-resume" in x["Arguments"]]
    assert len(resumers) == 1, "No unique actual resumer; do not invent timing results"
    resumer = resumers[0]
    assert len(children) == 2 and children[0]["Id"] != resumer["Id"]
    samples = [x for x in records(resumer.get("RawStdout") or "") if x.get("Boundary") == "joint_operation_timing"]
    assert samples, "The actual resumer emitted no named timing records"
    assert not any(x.get("Boundary") == "joint_operation_timing" for x in records(children[0].get("RawStdout") or ""))

    source_paths = [
        "BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs",
        "BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalJointRepair.cs",
        "BookOfEternityClient/Services/MortalJointRepairCheckpoint.cs",
        "BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs",
    ]
    locations = {}
    for path in source_paths:
        for number, line in enumerate((repo / path).read_text(encoding="utf-8-sig").splitlines(), 1):
            match = re.search(r'MeasureOperation\("([^"]+)"\)', line)
            if match:
                locations.setdefault(match[1], []).append({"Path": path, "Line": number})

    pending = []
    intervals = []
    last_elapsed = -1.0
    limited = False
    for expected, sample in enumerate(samples, 1):
        assert sample["Sequence"] == expected and sample["PID"] == resumer["Id"]
        elapsed = sample["TotalElapsedMilliseconds"]
        ms = sample["Milliseconds"]
        assert isinstance(elapsed, (float, int)) and not isinstance(elapsed, bool) and math.isfinite(elapsed)
        assert isinstance(ms, (float, int)) and not isinstance(ms, bool) and math.isfinite(ms)
        assert elapsed >= last_elapsed and ms >= 0
        last_elapsed = elapsed
        if sample["State"] == "limited":
            assert sample["Operation"] == "output-limit" and expected == 1025 and sample["Limit"] == 1024
            assert sample is samples[-1]
            limited = True
            continue
        assert sample["Operation"] in locations, "Unknown nonliteral operation name"
        if sample["State"] == "begin":
            assert ms == 0
            pending.append(sample)
        else:
            assert sample["State"] == "end" and pending
            begin = pending.pop()
            assert begin["Operation"] == sample["Operation"], "Non-LIFO spans; investigate rather than mispair"
            assert elapsed >= begin["TotalElapsedMilliseconds"]
            # Scope elapsed excludes its own output observer calls. Both clocks
            # are monotonic, but parent wait/cleanup clocks are not this clock.
            assert ms <= elapsed - begin["TotalElapsedMilliseconds"] + 1.0
            intervals.append({
                "Operation": sample["Operation"], "BeginSequence": begin["Sequence"],
                "EndSequence": sample["Sequence"], "Milliseconds": ms,
                "BeginCollectorMilliseconds": begin["TotalElapsedMilliseconds"],
                "EndCollectorMilliseconds": elapsed,
            })
    aggregated = []
    for operation in sorted({x["Operation"] for x in intervals}):
        durations = [x["Milliseconds"] for x in intervals if x["Operation"] == operation]
        aggregated.append({
            "Operation": operation, "CompletedCount": len(durations),
            "TotalMilliseconds": sum(durations), "MeanMilliseconds": statistics.mean(durations),
            "MinMilliseconds": min(durations), "MaxMilliseconds": max(durations),
            "SourceLocations": locations[operation],
        })
    aggregated.sort(key=lambda x: (-x["TotalMilliseconds"], x["Operation"]))
    last_normal = next(x["TotalElapsedMilliseconds"] for x in reversed(samples) if x["State"] != "limited")
    unfinished = [{
        "Operation": x["Operation"], "BeginSequence": x["Sequence"],
        "BeginCollectorMilliseconds": x["TotalElapsedMilliseconds"],
        "CompletedDurationMilliseconds": None,
        "ObservedCollectorWindowMilliseconds": last_normal - x["TotalElapsedMilliseconds"],
        "SourceLocations": locations[x["Operation"]],
    } for x in pending]
    current_dlls = []
    for binary in prep["Binding"]["Binaries"]:
        path = repo / binary["Path"]
        assert path.stat().st_size == binary["Bytes"] and digest(path.read_bytes()) == binary["SHA256"]
        current_dlls.append(binary)
    seed = outcome["SeedBinding"]
    artifact = repo / seed["ArtifactRelativePath"]
    assert digest((artifact / "manifest.json").read_bytes()) == seed["ManifestSha256"]
    for member in seed["PayloadFiles"]:
        raw = (artifact / "payload" / member["path"]).read_bytes()
        assert len(raw) == member["length"] and digest(raw) == member["sha256"]
    assert sorted(p.relative_to(artifact / "payload").as_posix() for p in (artifact / "payload").rglob("*") if p.is_file()) == sorted(x["path"] for x in seed["PayloadFiles"])
    assert outcome["BinariesUnchanged"]
    report = {
        "SchemaVersion": 1, "CompiledSource": outcome["CompiledSource"], "MetadataHead": outcome["MetadataHead"],
        "Run": outcome["Run"], "CaseOutcome": outcome["Outcome"], "CaseQualified": outcome["CaseQualified"],
        "TestDuration": outcome["TestDuration"], "RunnerWallTime": outcome["Summary"]["WallTime"],
        "ResumerPID": resumer["Id"], "ResumerExitCode": resumer["ExitCode"],
        "ResumerFailure": resumer.get("Failure"), "SharedBudgetChecks": outcome["SharedBudgetChecks"],
        "RecordCount": len(samples), "OutputLimited": limited, "SequenceContiguous": True,
        "AllRecordsBoundToActualResumer": True, "StackPairingVerified": True,
        "LastCollectorMilliseconds": last_elapsed, "LastNormalCollectorMilliseconds": last_normal,
        "CompletedIntervals": intervals,
        "Operations": aggregated, "Unfinished": unfinished,
        "InnermostObservedUnfinished": unfinished[-1] if unfinished else None,
        "ObservedLiteralOperations": sorted({x["Operation"] for x in samples if x["State"] != "limited"}),
        "InstrumentedLiteralOperationCount": sum(map(len, locations.values())),
        "RawArchive": archive_path.name, "RawArchiveSHA256": outcome["ArchiveSHA256"],
        "RawArchiveCrcValid": True, "RawArchiveIndex": archive_index, "RawStreamBindings": stream_bindings,
        "SeedManifestSha256": seed["ManifestSha256"], "CurrentSeedPayloadVerified": True,
        "CurrentBinariesVerified": current_dlls, "FixtureCleanup": outcome["FixtureCleanup"],
        "ChildrenEofAndJoined": all(x["Eof"] and x["Joined"] for x in children),
        "ChildrenCleanupFailures": [x.get("CleanupFailure") for x in children if x.get("CleanupFailure")],
        "ParentGameReportCount": len(outcome["ParentGameReports"]),
        "LastIndependentStageRecord": outcome["Children"][children.index(resumer)]["Stages"][-1] if outcome["Children"][children.index(resumer)]["Stages"] else None,
        "OriginalWindowsFive": "0/5 PASS; not rerun by this investigation", "Linux": "UNRUN locally",
        "Limits": [
            "One instrumented run. Scope times are inclusive; nested rows must not be added as independent wall time.",
            "An end boundary means the scope ended, including exception unwinding; it does not certify operation success.",
            "Begin-only spans have no measured completed duration. The 600-second parent wait is a different clock and is not assigned to an unfinished method.",
            "An observed collector window is the difference between two output timestamps, including observer overhead; it is not the measured duration of a completed scope.",
            "If the output limit is reached, open scopes describe the last observation, not necessarily the operation active at timeout.",
            "Instrumentation preserves calls and assertions but adds observer/serialization overhead. No uninstrumented speedup or fix is claimed.",
            "Named operation durations localize code cost; they do not separate CPU, filesystem latency, scanning or environment effects.",
        ],
    }
    return report


def markdown(report):
    lines = [
        "# Именованные замеры восстановления — T297", "",
        f"Один фактический запуск: `{report['Run']}`. Результат случая: **{report['CaseOutcome']}**; "
        f"тест {report['TestDuration']}, весь запуск {report['RunnerWallTime']}.", "",
        f"Исходники: `{report['CompiledSource']}`; сохранённое состояние запуска: `{report['MetadataHead']}`. "
        f"PID восстановления: {report['ResumerPID']}. "
        f"Получено {report['RecordCount']} записей, ограничение вывода {'достигнуто' if report['OutputLimited'] else 'не достигнуто'}. "
        f"Последняя запись по часам коллектора: {report['LastCollectorMilliseconds']/1000:.6f} с.", "",
        "## Завершённые интервалы", "",
        "Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. "
        "Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.", "",
        "| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |",
        "| --- | ---: | ---: | ---: | ---: |",
    ]
    for value in report["Operations"]:
        lines.append(f"| `{value['Operation']}` | {value['CompletedCount']} | {value['TotalMilliseconds']/1000:.6f} | {value['MeanMilliseconds']/1000:.6f} | {value['MaxMilliseconds']/1000:.6f} |")
    lines += ["", "## Начатые интервалы без записи конца", ""]
    if report["Unfinished"]:
        lines += ["Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.", "",
                  "| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |",
                  "| --- | ---: | ---: | ---: |"]
        for value in report["Unfinished"]:
            lines.append(f"| `{value['Operation']}` | {value['BeginSequence']} | {value['BeginCollectorMilliseconds']/1000:.6f} | {value['ObservedCollectorWindowMilliseconds']/1000:.6f} |")
        lines += ["", "Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода."]
        lines += ["", f"Самый внутренний наблюдавшийся незавершённый участок: **`{report['Unfinished'][-1]['Operation']}`**."]
        if report["OutputLimited"]:
            lines += ["Лимит вывода достигнут: это последнее наблюдение, а не доказательство участка, активного при тайм-ауте."]
    else:
        lines += ["Все начатые именованные интервалы имеют запись конца."]
    lines += ["", "## Проверка и границы вывода", "",
              "Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. "
              "Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. "
              "Текущие три DLL и все файлы исходного снимка сверены с сохранённой привязкой.", "",
              f"Сырой архив: [{report['RawArchive']}]({report['RawArchive']}); SHA-256 `{report['RawArchiveSHA256']}`. "
              "Полные интервалы, ссылки на строки исходников и индекс архива: [T297-timings.json](T297-timings.json).", "",
              f"EOF/join обоих процессов: {report['ChildrenEofAndJoined']}; ошибок завершения контейнеров: {len(report['ChildrenCleanupFailures'])}; "
              f"отчётов с игровым результатом: {report['ParentGameReportCount']}.", ""]
    last_stage = report["LastIndependentStageRecord"]
    if last_stage:
        lines += [f"Отдельный существующий канал стадий после исчерпания именованного вывода всё ещё зафиксировал "
                  f"`{last_stage['Stage']}/{last_stage['Transition']}` "
                  f"(`{last_stage.get('Checkpoint')}`) на {last_stage['ElapsedMilliseconds']/1000:.6f} с своих часов. "
                  "Это подтверждает более позднюю стадию, но не восстанавливает потерянные длительности методов и не является игровым PASS.", ""]
    for value in report["FixtureCleanup"]:
        lines.append(f"- Удаление собственного тестового корня: {value['OwnedFixtureRemoved']}; отсутствие подтверждено: {value['PathAbsentOnParentCheck']}.")
    lines += ["", "Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. "
              "Оптимизаций и увеличения лимитов в этом блоке нет. Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[5])
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--refresh-derived", action="store_true", help="Refresh only derived reports after an offline report clarification; never changes raw evidence")
    args = parser.parse_args()
    assert not (args.check and args.refresh_derived)
    report = build_report(args.repo)
    output = args.repo / "specs/1536-complete-wound-materialization/recovery/crash5"
    json_path = output / "T297-timings.json"
    md_path = output / "T297-timings.md"
    content = markdown(report)
    if args.check:
        assert load(json_path) == report
        assert md_path.read_text(encoding="utf-8-sig") == content
    else:
        if args.refresh_derived:
            previous = load(json_path)
            assert previous["Run"] == report["Run"] and previous["RawArchiveSHA256"] == report["RawArchiveSHA256"]
        else:
            assert not json_path.exists() and not md_path.exists(), "Use explicit --refresh-derived for a derived-report clarification"
        json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        md_path.write_text(content, encoding="utf-8")
    print(json.dumps({key: report[key] for key in ("Run", "CaseOutcome", "RecordCount", "OutputLimited", "InnermostObservedUnfinished")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
