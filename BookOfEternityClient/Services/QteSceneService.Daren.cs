using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.UI;
using Spectre.Console;

namespace BookOfEternityClient.Services;

public sealed partial class QteSceneService
{
    private const string DarenRouteId = "daren_qte_showcase";
    private const string DarenScoreMetric = "normalized_score";
    private const string DarenStealthMetric = "stealth";
    private const string DarenLootMetric = "loot";
    private const string DarenPursuitMetric = "pursuit_control";
    private const string DarenEvidenceMetric = "evidence";
    private const string DarenHideoutMetric = "hideout_safety";
    private const string DarenTerminalOutcomeId = "daren_hideout_return";

    public static DarenShowcaseRouteDefinition GetDarenShowcaseRoute() =>
        new()
        {
            RouteId = DarenRouteId,
            Beats = BuildDarenBeats(),
            Offer = BuildDarenOffer(),
            EndingTiers = DarenQteRewardProfileService.EndingTiers
        };

    public DarenShowcaseAttemptState StartDarenShowcaseAttempt()
    {
        var offer = BuildDarenOffer();
        return new DarenShowcaseAttemptState
        {
            AttemptId = $"{DarenRouteId}_{Guid.NewGuid():N}",
            State = "Active",
            ActiveScene = new ActiveQteSceneState
            {
                Offer = offer,
                CurrentChapterId = offer.StartChapterId,
                AcceptedAtTurn = 0,
                ScoreState = BuildInitialScoreState(offer.ScoreModel)
            },
            FeedbackTitle = "Ограбление поместья Дареном",
            Feedback = "Дарен начинает отдельную QTE-вылазку за магическим посохом.",
            BoundaryNotice = DarenBoundaryNotice,
            RewardNotice = DarenRewardNotice
        };
    }

    public async Task<QteActionResolution> ResolveDarenShowcaseActionAsync(
        DarenShowcaseAttemptState attempt,
        string actionId,
        string? submittedGrade,
        DateTime? completedAtUtc = null)
    {
        if (!string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Daren showcase attempt is not active.");

        var active = attempt.ActiveScene ?? throw new InvalidOperationException("Daren showcase scene is not active.");
        var offer = active.Offer ?? throw new InvalidOperationException("Daren showcase route is missing.");
        var chapter = offer.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
        if (chapter == null)
            throw new InvalidOperationException($"Daren showcase chapter '{active.CurrentChapterId}' not found.");

        var action = chapter.Actions.FirstOrDefault(item =>
            string.Equals(item.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action == null)
            throw new InvalidOperationException($"Daren showcase action '{actionId}' not found.");

        var grade = ResolveBrowserSubmittedGrade(action, submittedGrade);
        var target = grade switch
        {
            QteGrade.Success => action.Routing.Success,
            QteGrade.Partial => action.Routing.Partial,
            _ => action.Routing.Fail
        };
        var resultText = ResolveResultText(action, grade);
        if (grade == QteGrade.Fail)
            attempt.HadUnsafeRouteFailure = true;
        ApplyScoreDeltas(active.ScoreState, action, grade);

        QteActionResolution resolution;
        if (!string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
        {
            var normalizedScore = ResolveDarenNormalizedScore(active.ScoreState);
            var ending = DarenQteRewardProfileService.ResolveEnding(
                reachedHideout: !attempt.HadUnsafeRouteFailure,
                normalizedScore);
            var scoreSummary = BuildDarenFinalScoreSummary(offer.ScoreModel, active.ScoreState, ending);
            var profileResult = await new DarenQteRewardProfileService(_fs)
                .RecordCompletionAsync(ending, completedAtUtc ?? DateTime.UtcNow);
            var rewardMessage = ending.GrantsReward
                ? profileResult.Message
                : ending.RewardExplanation;
            var summary = BuildDarenCompletionSummary(ending, rewardMessage, scoreSummary);

            resolution = new QteActionResolution
            {
                State = "Completed",
                QteId = offer.QteId,
                ChapterId = chapter.ChapterId,
                ActionId = action.ActionId,
                Grade = grade.ToString().ToLowerInvariant(),
                ResultText = resultText,
                Completion = new QteSceneCompletion
                {
                    QteId = offer.QteId,
                    OutcomeId = ending.OutcomeId,
                    Summary = summary,
                    Response = new GameResponse
                    {
                        Response = $"{ending.DisplayName}. {ending.Summary} {ending.Epilogue} {rewardMessage}"
                    },
                    ScoreSummary = scoreSummary
                }
            };

            attempt.State = "Completed";
            attempt.LastCompletion = resolution.Completion;
            attempt.Ending = new DarenShowcaseEnding(
                ending.TierId,
                ending.DisplayName,
                ending.NormalizedScore,
                ending.InkFeatherBonus,
                ending.GrantsReward,
                ending.Epilogue,
                rewardMessage,
                rewardMessage);
            attempt.FeedbackTitle = ending.DisplayName;
            attempt.Feedback = $"{ending.Summary} {ending.Epilogue} {rewardMessage}";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.NextChapterId))
                throw new InvalidOperationException($"Daren showcase action '{action.ActionId}' has no next chapter or terminal outcome.");

            active.CurrentChapterId = target.NextChapterId;
            resolution = new QteActionResolution
            {
                State = "Active",
                QteId = offer.QteId,
                ChapterId = chapter.ChapterId,
                ActionId = action.ActionId,
                Grade = grade.ToString().ToLowerInvariant(),
                ResultText = resultText,
                NextChapterId = target.NextChapterId
            };

            attempt.FeedbackTitle = "Следующий участок";
            attempt.Feedback = resultText;
        }

        attempt.LastResolution = resolution;
        return resolution;
    }

    public async Task RunDarenShowcaseModeAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Panel(new Markup(
                "[bold cyan]Ограбление поместья Дареном[/]\n\n" +
                "Отдельная QTE-вылазка: обычная глава не меняется. Лучший итог сохраняет бонус Чернильных Перьев для будущей новой игры."))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices("Начать вылазку", "Выйти"));
            if (!selected.Contains("Начать", StringComparison.OrdinalIgnoreCase))
                return;

            var attempt = StartDarenShowcaseAttempt();
            while (string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var active = attempt.ActiveScene;
                var offer = active.Offer!;
                var chapter = offer.Chapters.First(item =>
                    string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
                ShowChapterPrelude(offer, chapter, active.ScoreState);

                var action = chapter.Actions.Single();
                var grade = await RunCheckAsync(action);
                var resolution = await ResolveDarenShowcaseActionAsync(attempt, action.ActionId, GradeKey(grade));
                await ShowIntermediateResultAsync(offer, chapter, action, grade);

                if (resolution.Completion == null)
                    continue;

                RenderDarenCompletion(attempt, resolution.Completion);
            }

            var next = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Что дальше?[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices("Повторить вылазку", "Выйти"));
            if (!next.Contains("Повторить", StringComparison.OrdinalIgnoreCase))
                return;
        }
    }

    private static IReadOnlyList<DarenShowcaseBeat> BuildDarenBeats() =>
    [
        new("approach_manor", "Подступ к поместью",
            """
            Ночь у поместья не была тихой: она шуршала мокрой травой, капала с чёрных листьев и шептала под стеной так низко, будто сама земля просила Дарена не поднимать головы. Он лежал в сырой ложбине у основания каменной кладки, прижимая плечо к холодному мху, и считал не удары сердца, а шаги патруля. Два фонаря расходились над калиткой, третий задерживался у караульной будки, и каждый жёлтый круг света резал туман так уверенно, словно стража уже знала, где искать вора.

            Дарен медленно втянул воздух сквозь зубы и почувствовал вкус ржавчины на губе: пока полз через канаву, он задел щёку о старую проволоку. Боль была полезной, она держала мысли острыми. За стеной ждало поместье с запертыми галереями, сонными ключниками и посохом, ради которого люди богаче Дарена ставили печати на двери, а люди беднее шли под нож. Он не пришёл сюда за славой; слава слишком громко ходит по мостовым. Ему нужна была добыча, тишина и такая дорога назад, которую утром нельзя будет прочитать по грязи.

            Слева от калитки темнела старая липа. Её корни вылезали из земли, как пальцы мертвеца, и бросали на стену рваную тень, почти совпадавшую с провалом между двумя фонарями. Дарен видел этот провал уже три раза: короткий вздох темноты, потом свет возвращался и гладил мокрые камни, траву, железные петли калитки. Если скользнуть слишком рано, патруль заметит движение краем глаза. Если опоздать, придётся бежать через открытый подступ, и вся ночь начнётся не с кражи, а с чужого крика.

            Он согнул колено под себя, проверил ладонью ножны, ремень, тонкий моток лески под плащом. Тело хотело дрожать от холода, но Дарен заставил дрожь уйти в пальцы: пусть дрожат пальцы, пока ноги помнят, куда ставить шаг. У караульной будки один страж засмеялся, другой ответил вполголоса, и фонарь у калитки качнулся ближе к липе. На миг в этом качании открылось всё, что ждало дальше: стена, мокрая кора, тень, один вдох без права на ошибку.

            «Не к свету», — прошептал Дарен так тихо, что слова остались на траве. Он поднялся на локтях, перенёс вес на ладони и выбрал тот самый промежуток, где старая липа могла принять его за свою тень.
            """),
        new("informant_parley", "Шёпот Миры",
            """
            После тени у старой липы Дарен не пошёл сразу к стене. Он срезал вдоль канавы, где дождевая вода несла чёрные листья, и добрался до задней дороги поместья так низко, будто сам стал частью мокрой земли. Навес над заброшенным воротным столбом скрипел на ветру; под ним пахло ржавыми цепями, лошадиным потом и гнилой соломой. Здесь фонари главного двора уже не доставали до лиц, но каждый звук уходил далеко: к караульне, к галерее, к тем окнам, за которыми ради посоха держали бодрствующую стражу.

            Мира Ночная Нить ждала в самом тёмном углу навеса. Дарен заметил её раньше, чем она позволила себя увидеть: узкая ладонь на рукояти ножа, плечо у мокрой балки, зелёная лента на запястье, потемневшая от дождя до цвета речной тины. Когда-то такая лента означала условный знак между ними, потом долг, потом молчание длиннее зимы. Мира умела хранить чужие ключи в памяти и чужие имена на языке, но своё доверие всегда держала глубже кошеля.

            — Ты пришёл с запахом стены, — сказала Мира, не выходя из тени. — И с чужим фонарём за спиной.

            Дарен остановился так, чтобы видеть дорогу и её пальцы одновременно. Усмешка тронула губы, но не дошла до глаз.

            — Фонарь смотрел не туда. Я помог ему не передумать.

            — Всё ещё шутишь, когда можешь умереть от одного громкого вдоха.

            — Всё ещё считаешь мои вдохи? — спросил Дарен. Старое воспоминание шевельнулось между ними, тёплое и злое, как уголь под пеплом. Он задавил его прежде, чем оно стало слабостью. — Мне нужен пароль у галереи и смена у внутренней двери. В письме я был достаточно вежлив.

            Мира шагнула ближе. Дождь сорвался с края навеса и разбился у её сапога. На лице информаторки не было ни улыбки, ни испуга, только та неподвижная внимательность, с которой уличные люди слушают нож, ещё не вынутый из ножен.

            — Вежливость не закрывает рот свидетелям, Дарен. Если стража найдёт мой источник, они сперва спросят про слух, потом про меня, а потом капитан Орвальд поведёт погоню не за тенью, а за именем. Мне это имя дорого.

            — Твоё или моё?

            — То, которое останется жить. — Мира подняла запястье с мокрой лентой, будто показывала клятву, испорченную дождём. — У галереи сегодня стоит старый Лукьян. Он сонный, но не слепой. Пароль меняли после заката. Я скажу больше, чем продаю обычно, если пойму, что ты не приведёшь стражников к моему порогу.

            Дарен слышал, как за дальним поворотом кашлянул караульный. Время сжалось до капель, падающих с навеса: одна, другая, третья. Можно было купить слух монетой, можно было прижать Миру к балке и вырвать половину правды силой, но оба пути оставляли след. А ему нужна была не только дверь. Ему нужен был человек, который после кражи не назовёт его направление.

            — Ты хочешь доказательство, — тихо сказал он.

            — Я хочу ответ, который знает только тот, кто слушал правильную смену, а не болтал с пьяным конюхом. Назови её точно, Дарен. Тогда я поверю, что мой шёпот не станет верёвкой у меня на горле.

            Мира отступила на полшага, освобождая ему дорогу к разговору и закрывая дорогу к отступлению. В её взгляде не было просьбы. Только договор, старый долг и тонкая грань между доверием и криком, который мог разбудить всю заднюю дорогу.
            """),
        new("gadget_infiltration", "Крюк и леска",
            """
            От навеса Миры Дарен ушёл не сразу к свету, а к той стороне башни, где камень казался мёртвым даже для луны. Здесь двор сужался между стеной и хозяйственными пристройками, как горло, готовое сомкнуться на чужом дыхании. Над ним темнел балкон второго яруса: узкая чёрная губа с мокрыми перилами, слишком высокая для прыжка и слишком открытая для ошибки. Внизу ходили фонари караула, и жёлтый свет то облизывал булыжники, то отступал, оставляя после себя влажный блеск, похожий на след свежей крови.

            Дарен прижал плечо к холодному камню башни и дал телу привыкнуть к этой стуже. Камень не просто морозил кожу под плащом; он забирал лишнее движение, заставлял ребра дышать мельче, а пальцы работать точнее. Слова Миры о сонном Лукьяне ещё держались в памяти, но здесь пароль уже ничего не открывал. Здесь решали не чужие имена, а металл, леска и то, насколько тихо вор умеет попросить высоту принять его.

            Он вынул складной крюк из внутреннего кармана. Железные лапы были обмотаны тёмной кожей, чтобы не дать лишнего звона, но Дарен всё равно раскрыл их медленно, по одному суставу, будто будил опасного зверя. Пружина дрогнула под большим пальцем. Леска, тонкая и прочная, легла на ладонь прохладной змеёй; в ней жила память о соли, крысиных мостках и ночах, когда одно неверное натяжение стоило человеку имени. Дарен провёл пальцами по узлам, проверил, не зацепилась ли нить за пряжку, и опустился на одно колено, пряча силуэт ниже выбитого каменного уступа.

            Во дворе скрипнула дверь. Один страж вышел к бочке с дождевой водой, поднял фонарь и лениво повёл светом вдоль стены. Луч не дошёл до Дарена на ладонь, остановился на трещине в кладке и сполз вниз, будто сам боялся смотреть выше. В этот миг балкон показался ближе, но и громче: мокрое дерево могло принять крюк мягко, а могло ответить сухим стуком на весь двор. Если железо ударит по перилам, первый оклик поднимет караул раньше, чем Дарен успеет оторваться от земли.

            Он не бросал сразу. Сначала Дарен подтянул леску к запястью, перенёс вес с колена на ступню и нашёл плечом такую точку у башни, где холодный камень держал его ровно. Дыхание стало счётом, но не тем счётом, который ведут вслух: вдох, шаг стража по гравию, выдох, капля с балкона, пауза, скрип ремня у караульного. Вор видел путь заранее: крюк должен лечь за внутренний край перил, лапы раскрыться без звона, леска натянуться не рывком, а живой жилой, и только потом тело пойдёт вверх по стене, оставив двор искать тень там, где её уже нет.

            На дальнем крыльце кашлянули. Фонарь качнулся, и свет полез по башне, медленно, как ищейка по следу. Дарен поднял руку, чувствуя, как металл в крюке делается тяжёлым от последнего мгновения. Пальцы разжались не полностью, а ровно настолько, чтобы пружина повернула складные лапы в нужную сторону. Оставалось метнуть крюк в темноту над балконом, зацепить его мягко, без крика железа, и подняться до того, как двор поймёт, что уже слушает не дождь, а чужую леску.
            """),
        new("stealth_crossing", "Галерея без звука",
            """
            Балконное окно впустило Дарена не в комнату, а в длинную галерею, где ночь казалась старше самого поместья. За его спиной леска ещё дрожала после подъёма, но он уже придержал её ладонью и дал металлу замолчать у запястья. Перед ним тянулся пол из тёмного дерева: доски лежали ровно, слишком ровно, будто каждая берегла в себе отдельный скрип и ждала веса чужого сапога. По стенам висели портреты прежних хозяев; их лица выплывали из пыли и треснувшего лака, бледные, вытянутые, с глазами, которые в полосе света казались не нарисованными, а проснувшимися.

            Дарен не любил такие залы. В переулке тень честнее: она прячет, пока ты умеешь быть частью грязи и кирпича. Галерея же слушала всем сразу: рамами, стеклом, иссохшим паркетом, тяжёлыми портьерами, чёрными щелями под дверями. Даже пыль здесь не лежала спокойно; она поднималась от малейшего движения, ловила тонкий луч фонаря и превращала воздух в серую паутину. Где-то впереди был кабинет с посохом, а между ним и Дареном — не замок, не стена, а тишина, которая могла предать громче караульного рога.

            У дальней арки спал страж. Он сидел в кресле боком к проходу, подбородок упал на грудь, пальцы всё ещё держали древко короткого копья. Фонарь стоял у его ноги и давал узкую полоску жёлтого света поперёк пола, как черту, проведённую между живыми и теми, кто уже стал имуществом дома. Страж дышал тяжело: вдох цеплялся за горло, выдох шёл с сухим храпом и шевелил седой ус. На каждый третий выдох копьё едва стукалось о подлокотник. Этот слабый звук спасал Дарена, потому что давал счёт, но обещал смерть, если чужой шум ляжет между ударами.

            Дарен согнул колени и опустил центр тяжести так низко, чтобы плащ перестал тянуть плечи назад. Сначала он проверил ближайшую доску носком сапога, не наступая, только касаясь кожи к дереву. Пол ответил почти неслышным вздохом. Он замер, удержал дыхание в ребрах, дождался храпа стража и перенёс вес на внешнюю сторону ступни. Пальцы левой руки скользнули по стене, нашли холодный край рамы; портрет под ладонью был липким от пыли, и Дарену почудилось, что мёртвый барон хочет схватить его за запястье. Он не отдёрнул руку. Вор не имеет права пугаться вещей, которые молчат.

            Шаг за шагом он двинулся вдоль стены. Там, где свет резал галерею, Дарен остановился и посмотрел не на фонарь, а на пыль в его луче: если пройти прямо, серое облако выдаст движение раньше, чем доска выдаст скрип. Он снял с пояса тонкую тряпицу, прижал её к пряжке, чтобы металл не звякнул, и чуть развернул плечо. Следующий шаг должен был лечь на тёмную жилу между половицами; дальше — короткий перенос веса к колонне; потом — пауза у портьеры, пока страж снова вдохнёт с хрипом. За портьерой начиналась служебная дверь, а за ней коридор к кабинету.

            В глубине зала что-то шевельнулось. Не человек — старый дом, скрученный сыростью, выпустил из дерева тонкий треск. Страж вздрогнул, фонарь дрогнул вместе с ним, и полоска света поползла по полу к носку Даренова сапога. Дарен остановил даже мысль. Он сжал пальцы на пыльной раме, медленно выдохнул через нос и приглушил себя до пустоты: ни плаща, ни кожи, ни ножен, ни сердца. Если страж проснётся сейчас, Лукьян у служебной двери получит не смутный слух, а живой след к кабинету. Если Дарен пройдёт чисто, галерея останется только комнатой с портретами, которые никому не умеют рассказывать правду.

            Фонарь застыл. Храп вернулся, неровный, но сонный. Дарен отпустил раму по одному пальцу, выбрал следующий шаг и приготовился пересечь жёлтую полосу так, чтобы каждый звук был приглушён до тишины, а сама галерея не поняла, что через неё уже проходит вор.
            """),
        new("guard_interrogation", "Ключник в галерее", "У служебной двери Дарена останавливает Лукьян Седой Ключник, старый страж с фонарём ниже глаз и связкой дверных колец на руке. Вопрос звучит тихо, но подозрение уже стоит между ними: ответ решит, станет ли Лукьян свидетелем или случайной тенью."),
        new("lock_pick", "Замок кабинета", "У двери кабинета Дарен слышит, как старый замок отвечает отмычке сухими штифтами. Ему нужно открыть проход без царапин и шума, потому что любой след на накладке приведёт стражу к посоху."),
        new("rune_memory", "Руны на дверце", "Дарен склоняется к дверце футляра, и синие руны вспыхивают на стекле так быстро, будто дом смотрит ему в глаза. Он должен запомнить узор и повторить его без ошибки, иначе защитный сигнал проснётся раньше кражи."),
        new("ward_steward_parley", "Голос Ренары", "У погасших рун к Дарену обращается Ренара Вардовая, управляющая печатями дома, хотя её лицо остаётся только в холодном стекле футляра. Она спрашивает, зачем чужая рука тронула посох; ответ должен усыпить дом, а не дать сигналу имя вора."),
        new("physical_pressure", "Тяжёлая решётка", "После голоса Ренары футляр с посохом выходит из ниши, но над ним тяжёлая решётка начинает падать, давя Дарену на плечо. Ему нужно удержать железо до последнего дюйма: если оно сорвётся, грохот разнесёт тревогу по крылу."),
        new("timed_rhythm", "Пульс сигнализации", "В коридоре Дарен видит сигнальный кристалл, который бьёт красным светом по полу и стенам. Между вспышками остаются короткие паузы, и он должен двигаться в их ритме, пока тревога не поймала его тень."),
        new("route_decision", "Развилка в оранжерее", "В оранжерее перед Дареном раскрываются три выхода: мокрое стекло, служебная калитка и яркая арка. Ему нужно выбрать путь, который смоет след и не отдаст погоне направление, пока посох ещё можно вынести тихо."),
        new("staff_theft", "Кража посоха", "Дарен снимает посох с бархатных держателей, и вокруг него едва слышно качаются тонкие кольца с тревожным звоном. За спиной остаётся замок, чья царапина на накладке может привести стражу сюда, поэтому добычу нужно уложить на ремень без нового голоса."),
        new("pursuit", "Первый рывок", "За спиной Дарена распахивается зал, и капитан Орвальд Шпиль уже кричит во дворе, где открытое окно сужается в полоску ночи. Если Лукьян стал свидетелем у двери, этот рывок отдаст погоне лицо и имя; точный момент ещё может сбить их темп."),
        new("chase_chain", "Цепочка дворов", "Дарен несётся от выбранного в оранжерее выхода через задний двор, низкую стену, телегу и тёмную аллею, вспоминая маршрут как цепочку ударов сердца. Каждый прыжок и поворот должен сбить преследователей со следа, иначе погоня прочитает всю дорогу к мосту."),
        new("hideout_return", "Убежище под мостом", "Под мостом Дарен вжимается в своё убежище, где вода глушит шаги, а тайник ждёт посох под мокрым камнем. Теперь нужно спрятать добычу и зачистить след: если капитан Орвальд доведёт погоню до этого края, ночь станет опасной даже после кражи.")
    ];

    private static QteOffer BuildDarenOffer()
    {
        var beats = BuildDarenBeats();
        var chapters = new List<QteChapter>();
        for (var index = 0; index < beats.Count; index++)
        {
            var beat = beats[index];
            var nextBeat = index + 1 < beats.Count ? beats[index + 1].BeatId : null;
            chapters.Add(new QteChapter
            {
                ChapterId = beat.BeatId,
                Title = beat.Title,
                Narrative = beat.PlayerText,
                Actions = [BuildDarenAction(beat.BeatId, nextBeat)]
            });
        }

        return new QteOffer
        {
            QteId = DarenRouteId,
            Title = "Ограбление поместья Дареном",
            OfferText = "Дарен, хитрый вор из переулков Вечной Книги, идёт за магическим посохом в запертое поместье, где свет, стража и руны ждут ошибки.",
            IntroNarrative = "Это отдельная ночная вылазка Дарена: она не трогает текущую главу и не расходует обычный ход вашего героя.",
            DeclineHint = "Можно выйти в меню без последствий для обычной игры.",
            CinematicJustification = "Кража собрана как цепь коротких испытаний, чтобы каждый риск был виден прямо в сцене.",
            StartChapterId = "approach_manor",
            Chapters = chapters,
            TerminalOutcomes =
            [
                new QteTerminalOutcome
                {
                    OutcomeId = DarenTerminalOutcomeId,
                    Title = "Возвращение в убежище",
                    FinalNarrative = "Под мостом Дарен закрывает тайник; ночь подсчитывает тишину, улики и жар погони.",
                    GmSummary = "Client-owned Daren showcase completion; not a GM-authored campaign QTE offer and no campaign state mutation.",
                    ResponseFragment = new JsonObject
                    {
                        ["response"] = "Под мостом Дарен закрывает тайник; ночь подсчитывает тишину, улики и жар погони."
                    }
                }
            ],
            ScoreModel = BuildDarenScoreModel()
        };
    }

    private static QteAction BuildDarenAction(string beatId, string? nextBeatId)
    {
        var isTerminal = string.IsNullOrWhiteSpace(nextBeatId);
        var routing = new QteRouting
        {
            Success = isTerminal
                ? new QteBranchTarget { TerminalOutcomeId = DarenTerminalOutcomeId }
                : new QteBranchTarget { NextChapterId = nextBeatId },
            Partial = isTerminal
                ? new QteBranchTarget { TerminalOutcomeId = DarenTerminalOutcomeId }
                : new QteBranchTarget { NextChapterId = nextBeatId },
            Fail = isTerminal
                ? new QteBranchTarget { TerminalOutcomeId = DarenTerminalOutcomeId }
                : new QteBranchTarget { NextChapterId = nextBeatId }
        };

        return beatId switch
        {
            "approach_manor" => Action(
                beatId,
                "Выбрать тень у старой липы",
                "BranchChoice",
                Characteristics.Wisdom,
                2,
                DarenBranchChoiceConfig("success"),
                routing,
                "Дарен скользит в слепой промежуток между фонарями, и стена поместья принимает его без оклика.",
                "Сухая ветка ломается под каблуком, но Дарен успевает прижаться к стене, оставляя страже только сомнение.",
                "Дарен теряет драгоценный миг у освещённой калитки, и патруль начинает смотреть в его сторону.",
                DarenScoreDeltas(stealth: 4, evidence: -2)),
            "informant_parley" => Action(
                beatId,
                "Ответить Мире Ночной Нити",
                "PrecisionChoice",
                Characteristics.Wisdom,
                2,
                DarenDialoguePrecisionChoiceConfig(
                    "old_captain_shift",
                    ("old_captain_shift", "Назвать смену караула", "success", "Дарен подтверждает слух Миры о старшем карауле.", "Точный слух покупает доверие и имя погони."),
                    ("pay_for_rumor", "Заплатить за слух", "partial", "Дарен предлагает монету вместо пароля.", "Деньги помогут, но Мира оставит часть правды себе."),
                    ("threaten_contact", "Прижать информаторку", "fail", "Дарен требует ответ силой и торопит разговор.", "Угроза делает Миру свидетелем, а не союзницей.")),
                routing,
                "Мира Ночная Нить принимает точный пароль Дарена и шепчет, что Лукьян дремлет у галереи, а Орвальд ведёт погоню сам.",
                "Мира берёт монету Дарена, но отвечает коротко: ключник устал, а имя капитана она оставляет за следующим долгом.",
                "Мира замолкает после угрозы Дарена; её взгляд обещает, что слух о наглом воре найдёт стражу быстрее него.",
                DarenScoreDeltas(stealth: 2, pursuit: 2, evidence: -1)),
            "gadget_infiltration" => Action(
                beatId,
                "Запустить складной крюк",
                "ChargeRelease",
                Characteristics.Dexterity,
                3,
                null,
                routing,
                "Крюк ложится на балкон мягко, и Дарен поднимается над двором, пока леска молчит в ладони.",
                "Крюк держит, но леска звенит по камню; Дарен замирает на балконе, слушая двор.",
                "Крюк срывается с края; шум будит двор, и Дарен успевает уйти в тень только после собачьего лая.",
                DarenScoreDeltas(stealth: 3, pursuit: 2)),
            "stealth_crossing" => Action(
                beatId,
                "Пройти галерею без шума",
                "StealthNoise",
                Characteristics.Dexterity,
                3,
                DarenStealthNoiseConfig(),
                routing,
                "Дарен переводит вес с доски на доску, проходит чисто и не оставляет галерее ни следа, ни проснувшегося дыхания.",
                "Один страж шевелится от скрипа; сомнение уже тянется к фонарю, но Дарен удерживает тишину до открытых глаз.",
                "Доска отвечает резким треском, и Дарен видит, как в дальнем крыле поднимается тревожный фонарь со свидетелем.",
                DarenScoreDeltas(stealth: 5, evidence: -2)),
            "guard_interrogation" => Action(
                beatId,
                "Успокоить Лукьяна у служебной двери",
                "PrecisionChoice",
                Characteristics.Persuasion,
                3,
                DarenDialoguePrecisionChoiceConfig(
                    "mira_phrase",
                    ("mira_phrase", "Передать фразу Миры", "success", "Дарен говорит тихую фразу, которую Лукьян ждал от ночной связной.", "Фраза превращает ключника в сонного союзника."),
                    ("late_order", "Сослаться на поздний приказ", "partial", "Дарен изображает посыльного с приказом по дому.", "Приказ звучит правдоподобно, но Лукьян запомнит лицо."),
                    ("hide_face", "Спрятать лицо", "fail", "Дарен пытается пройти мимо без ответа.", "Молчание для стража громче любого скрипа.")),
                routing,
                "Лукьян Седой Ключник узнаёт пароль Миры, отворачивает фонарь и оставляет Дарену чистую дверь к кабинету.",
                "Лукьян пропускает Дарена с сомнением, но его взгляд цепляется за плащ и уже ищет вторую встречу.",
                "Лукьян поднимает фонарь к лицу Дарена, и в галерее рождается свидетель, которого нельзя назвать случайным.",
                DarenScoreDeltas(stealth: 3, evidence: -2)),
            "lock_pick" => Action(
                beatId,
                "Выставить штифты замка",
                "LockPinSet",
                Characteristics.Dexterity,
                3,
                DarenLockPinSetConfig(),
                routing,
                "Штифты становятся ровно, и Дарен открывает кабинет без следа, так тихо, что пыль на ручке не дрожит.",
                "Замок сдаётся, но отмычка царапает накладку; Дарен уносит этот след вместе с тревогой.",
                "Замок щёлкает слишком громко, оставляя улику на накладке, и Дарен слышит, как за стеной меняется дыхание стражи.",
                DarenScoreDeltas(stealth: 3, evidence: -1)),
            "rune_memory" => Action(
                beatId,
                "Повторить узор защитных рун",
                "PatternMemory",
                Characteristics.Perception,
                3,
                DarenPatternMemoryConfig(),
                routing,
                "Дарен повторяет узор без дрожи, и руны гаснут одна за другой; дом молчит, оставляя футляр без крика.",
                "Одна руна трескается и оставляет след в стекле, но Дарен удерживает порядок знаков, пока дверь открыта.",
                "Руны вспыхивают тревожным светом, и Дарен понимает, что дом уже запомнил его прикосновение.",
                DarenScoreDeltas(loot: 3, evidence: -1)),
            "ward_steward_parley" => Action(
                beatId,
                "Ответить Ренаре Вардовой",
                "PrecisionChoice",
                Characteristics.Wisdom,
                4,
                DarenDialoguePrecisionChoiceConfig(
                    "false_seal",
                    ("false_seal", "Назвать ложную печать", "success", "Дарен выдаёт трещину за проверку старой печати.", "Дом любит точные слова и гасит лишний сигнал."),
                    ("promise_return", "Пообещать возврат", "partial", "Дарен обещает вернуть печать на место после осмотра.", "Обещание задержит тревогу, но Ренара запомнит голос."),
                    ("mock_house", "Спорить с домом", "fail", "Дарен бросает вызов голосу в стекле.", "Гордость будит варды быстрее любой ошибки.")),
                routing,
                "Ренара Вардовая принимает объяснение Дарена, и дом гасит лишнюю печать так, будто сам решил молчать.",
                "Ренара отпускает Дарена с холодным предупреждением: дом подождёт, но голос вора она уже держит в рунах.",
                "Ренара отвечает резким светом; дом узнаёт Дарена как нарушителя, и тревога получает почти человеческую волю.",
                DarenScoreDeltas(loot: 2, pursuit: 3, evidence: -2)),
            "physical_pressure" => Action(
                beatId,
                "Удержать тяжёлую решётку",
                "MashInput",
                Characteristics.Strength,
                3,
                DarenMashInputConfig(),
                routing,
                "Дарен держит решётку до последнего хода механизма, и футляр выходит из ниши без грохота.",
                "Железо проседает и бьёт Дарена по плечу, но посох уже свободен от каменной ниши.",
                "Решётка падает на камень с тяжёлым грохотом, и Дарену приходится хватать посох под шум тревоги.",
                DarenScoreDeltas(loot: 4, pursuit: 2)),
            "timed_rhythm" => Action(
                beatId,
                "Двигаться между ударами кристалла",
                "RhythmPulse",
                Characteristics.Speed,
                3,
                DarenRhythmPulseConfig(),
                routing,
                "Дарен проходит от тени к тени точно между ударами, сбивает кристалл с ритма и не оставляет света на сапоге.",
                "Один красный пульс цепляет сапог и оставляет след задержки, но Дарен вырывается из света до тревоги.",
                "Кристалл режет тишину звоном, и Дарен бежит дальше уже под просыпающуюся тревогу.",
                DarenScoreDeltas(stealth: 4, pursuit: 2)),
            "route_decision" => Action(
                beatId,
                "Выбрать выход через оранжерею",
                "PrecisionChoice",
                Characteristics.Perception,
                3,
                DarenPrecisionChoiceConfig(),
                routing,
                "Дарен выбирает влажный проход, где вода разбивает следы и делает погоню неуверенной.",
                "Служебная калитка выпускает Дарена быстро, но листья и грязь показывают погоне его направление.",
                "Дарен бросается к яркой арке, и свет на мгновение выдаёт его силуэт как след для погони во всём дворе.",
                DarenScoreDeltas(pursuit: 4, evidence: -2)),
            "staff_theft" => Action(
                beatId,
                "Удержать посох на ремне",
                "BalanceMeter",
                Characteristics.Dexterity,
                4,
                null,
                routing,
                "Посох ложится на ремень без звона и не оставляет нового следа; Дарен чувствует, как добыча становится частью шага.",
                "Один тонкий звон уходит под потолок, и этот шум стоит Дарену секунды, но он ловит баланс раньше хора подвесок.",
                "Посох бьёт по подвескам, и Дарену приходится рвануть прочь под голос тревоги от украденной добычи.",
                DarenScoreDeltas(loot: 5, stealth: 2)),
            "pursuit" => Action(
                beatId,
                "Рвануть в окно погони",
                "TimingBar",
                Characteristics.Speed,
                4,
                null,
                routing,
                "Дарен вылетает в окно до того, как стражи смыкают двор, и погоня теряет первый удар.",
                "Плащ мелькает в фонарях, и стража запоминает след, но Дарен всё же уходит через двор раньше сомкнутых рук.",
                "Дарен теряет шаг на подоконнике, и погоня получает его ритм почти вплотную за спиной.",
                DarenScoreDeltas(pursuit: 5, stealth: 1)),
            "chase_chain" => Action(
                beatId,
                "Повторить цепочку дворов",
                "PromptChain",
                Characteristics.Speed,
                4,
                null,
                routing,
                "Дарен проходит цепочку дворов чисто, и каждый прыжок стирает за ним ещё один след.",
                "Темп сбивается на мокрой телеге, но Дарен удерживается и оставляет погоне только рваный путь.",
                "Бочка разлетается под ногой, и Дарен слышит, как преследователи читают его след по шуму.",
                DarenScoreDeltas(pursuit: 4, evidence: -2)),
            "hideout_return" => Action(
                beatId,
                "Спрятать посох и зачистить след",
                "BranchChoice",
                Characteristics.Wisdom,
                3,
                DarenBranchChoiceConfig("success"),
                routing,
                "Дарен запечатывает посох в тайнике под мостом, чисто смывает след и оставляет погоню проходить выше.",
                "Посох спрятан, но один поспешный след остаётся у входа, и Дарену приходится ждать без дыхания.",
                "Убежище принимает добычу слишком шумно, и Дарен понимает, что тайник переживёт не каждую погоню.",
                DarenScoreDeltas(hideout: 6, evidence: -3)),
            _ => throw new InvalidOperationException($"Unknown Daren beat '{beatId}'.")
        };
    }

    private static QteAction Action(
        string beatId,
        string label,
        string checkType,
        string characteristic,
        int baseDifficulty,
        JsonObject? config,
        QteRouting routing,
        string successText,
        string partialText,
        string failText,
        Dictionary<string, List<QteScoreDelta>> scoreDeltas) =>
        new()
        {
            ActionId = $"{beatId}_action",
            Label = label,
            Check = new QteCheck
            {
                Type = checkType,
                BaseDifficulty = baseDifficulty,
                PrimaryCharacteristic = characteristic,
                Config = config
            },
            Routing = routing,
            SuccessText = successText,
            PartialText = partialText,
            FailText = failText,
            ScoreDeltas = scoreDeltas
        };

    private static QteScoreModel BuildDarenScoreModel() =>
        new()
        {
            Metrics =
            [
                new QteScoreMetricDefinition { Id = DarenScoreMetric, Label = "Счёт вылазки", Initial = 35, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenStealthMetric, Label = "Скрытность", Initial = 50, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenLootMetric, Label = "Добыча", Initial = 50, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenPursuitMetric, Label = "Контроль погони", Initial = 50, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenEvidenceMetric, Label = "Улики", Initial = 35, Min = 0, Max = 100, Visibility = "hidden" },
                new QteScoreMetricDefinition { Id = DarenHideoutMetric, Label = "Безопасность убежища", Initial = 50, Min = 0, Max = 100, Visibility = "always" }
            ],
            RankOrder = ["perfect_shadow", "clean_heist", "broken_trail", "shadow_on_the_run", "no_reward_failure"],
            Ranks =
            [
                DarenRank("perfect_shadow", "Идеальная тень", "Дарен уходит с посохом чисто, быстро и без следов.", 90),
                DarenRank("clean_heist", "Чистая кража", "Посох добыт, погоня отрезана, последствия управляемы.", 75),
                DarenRank("broken_trail", "Сорванный след", "Дарен сбивает погоню, но часть следов остаётся.", 55),
                DarenRank("shadow_on_the_run", "Тень в бегах", "Дарен выжил и ушёл, но вылазка получилась грязной.", 40),
                new QteScoreRankDefinition
                {
                    Id = "no_reward_failure",
                    Label = "Провал вылазки",
                    Summary = "Безопасный итог не достигнут: постоянная награда не записывается.",
                    Fallback = true
                }
            ]
        };

    private static QteScoreRankDefinition DarenRank(string id, string label, string summary, int threshold) =>
        new()
        {
            Id = id,
            Label = label,
            Summary = summary,
            AllOf = [new QteScoreThreshold { Metric = DarenScoreMetric, Op = ">=", Value = threshold }]
        };

    private static Dictionary<string, List<QteScoreDelta>> DarenScoreDeltas(
        int stealth = 0,
        int loot = 0,
        int pursuit = 0,
        int evidence = 0,
        int hideout = 0) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = DarenGradeDeltas(5, stealth, loot, pursuit, evidence, hideout),
            ["partial"] = DarenGradeDeltas(0, Math.Max(0, stealth / 2), Math.Max(0, loot / 2), Math.Max(0, pursuit / 2), evidence / 2, Math.Max(0, hideout / 2)),
            ["fail"] = DarenGradeDeltas(-8, -Math.Max(3, stealth), -Math.Max(2, loot), -Math.Max(2, pursuit), Math.Max(4, Math.Abs(evidence)), -Math.Max(2, hideout))
        };

    private static List<QteScoreDelta> DarenGradeDeltas(
        int normalizedScore,
        int stealth,
        int loot,
        int pursuit,
        int evidence,
        int hideout) =>
    [
        new QteScoreDelta { Metric = DarenScoreMetric, Delta = normalizedScore },
        new QteScoreDelta { Metric = DarenStealthMetric, Delta = stealth },
        new QteScoreDelta { Metric = DarenLootMetric, Delta = loot },
        new QteScoreDelta { Metric = DarenPursuitMetric, Delta = pursuit },
        new QteScoreDelta { Metric = DarenEvidenceMetric, Delta = evidence },
        new QteScoreDelta { Metric = DarenHideoutMetric, Delta = hideout }
    ];

    private static JsonObject DarenBranchChoiceConfig(string grade) =>
        new()
        {
            ["choiceGrade"] = grade
        };

    private static JsonObject DarenMashInputConfig() =>
        new()
        {
            ["keys"] = DarenStringArray("space"),
            ["durationMs"] = 3200,
            ["targetPresses"] = 13,
            ["partialThreshold"] = 0.55
        };

    private static JsonObject DarenPatternMemoryConfig() =>
        new()
        {
            ["alphabet"] = DarenStringArray("q", "w", "e", "space"),
            ["sequenceLength"] = 4,
            ["revealMs"] = 2400,
            ["inputTimeoutMs"] = 6500,
            ["allowedMistakes"] = 1
        };

    private static JsonObject DarenRhythmPulseConfig() =>
        new()
        {
            ["pulseCount"] = 5,
            ["beatIntervalMs"] = 640,
            ["hitWindowMs"] = 125,
            ["allowedMisses"] = 1,
            ["patternVariation"] = "swing"
        };

    private static JsonObject DarenDialoguePrecisionChoiceConfig(
        string correctChoiceId,
        params (string Id, string Label, string Grade, string Description, string Hint)[] choices)
    {
        var choiceArray = new JsonArray();
        foreach (var choice in choices)
        {
            choiceArray.Add(new JsonObject
            {
                ["id"] = choice.Id,
                ["label"] = choice.Label,
                ["grade"] = choice.Grade,
                ["description"] = choice.Description,
                ["hint"] = choice.Hint
            });
        }

        return new JsonObject
        {
            ["correctChoiceId"] = correctChoiceId,
            ["timeoutMs"] = 7000,
            ["timeoutGrade"] = "fail",
            ["choices"] = choiceArray
        };
    }

    private static JsonObject DarenPrecisionChoiceConfig()
    {
        var choices = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "wet_glass",
                ["label"] = "Влажная оранжерея",
                ["grade"] = "success",
                ["description"] = "Следы смоет вода из разбитых трубок.",
                ["hint"] = "Вода и тень скрывают направление."
            },
            new JsonObject
            {
                ["id"] = "servant_gate",
                ["label"] = "Служебная калитка",
                ["grade"] = "partial",
                ["description"] = "Быстрее, но на земле остаётся грязный след."
            },
            new JsonObject
            {
                ["id"] = "bright_arch",
                ["label"] = "Освещённая арка",
                ["grade"] = "fail",
                ["description"] = "Прямой выход виден из караульной."
            }
        };

        return new JsonObject
        {
            ["correctChoiceId"] = "wet_glass",
            ["timeoutMs"] = 6000,
            ["timeoutGrade"] = "fail",
            ["choices"] = choices,
            ["decoyHints"] = new JsonArray
            {
                new JsonObject
                {
                    ["choiceId"] = "servant_gate",
                    ["hint"] = "Быстрый проход не всегда самый чистый."
                }
            }
        };
    }

    private static JsonObject DarenStealthNoiseConfig() =>
        new()
        {
            ["durationMs"] = 6500,
            ["startingNoise"] = 14,
            ["dangerThreshold"] = 70,
            ["noiseDriftPerSecond"] = 9,
            ["recoveryPerInput"] = 12,
            ["allowedOverThresholdMs"] = 800,
            ["recoveryKey"] = "space",
            ["recoveryLabel"] = "приглушить шаг",
            ["warningLabel"] = "страж слышит шум",
            ["gradeThresholds"] = new JsonObject
            {
                ["successMaxNoise"] = 48,
                ["successMaxOverThresholdMs"] = 0,
                ["partialMaxNoise"] = 76,
                ["partialMaxOverThresholdMs"] = 850
            }
        };

    private static JsonObject DarenLockPinSetConfig() =>
        new()
        {
            ["pinCount"] = 3,
            ["pinWindows"] = new JsonArray
            {
                new JsonObject { ["pin"] = 1, ["min"] = 18, ["max"] = 32, ["label"] = "нижний штифт" },
                new JsonObject { ["pin"] = 2, ["min"] = 44, ["max"] = 58, ["label"] = "средний штифт" },
                new JsonObject { ["pin"] = 3, ["min"] = 68, ["max"] = 82, ["label"] = "верхний штифт" }
            },
            ["timerMs"] = 12000,
            ["pickDurability"] = 6,
            ["maxMistakes"] = 2,
            ["pinDriftPerSecond"] = 3,
            ["adjustKey"] = "q",
            ["setKey"] = "space",
            ["pinLabel"] = "штифт",
            ["durabilityLabel"] = "прочность отмычки",
            ["warningLabel"] = "замок шумит",
            ["gradeThresholds"] = new JsonObject
            {
                ["successMaxTimeMs"] = 6500,
                ["successMaxMistakes"] = 0,
                ["partialMaxTimeMs"] = 11000,
                ["partialMaxMistakes"] = 2
            }
        };

    private static JsonArray DarenStringArray(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }

    private static int ResolveDarenNormalizedScore(QteScoreState? scoreState)
    {
        var metric = scoreState?.Metrics.FirstOrDefault(item =>
            string.Equals(item.Id, DarenScoreMetric, StringComparison.OrdinalIgnoreCase));
        return metric == null ? 0 : Math.Clamp((int)Math.Round(metric.Value), 0, 100);
    }

    private static string BuildDarenCompletionSummary(
        DarenEndingResult ending,
        string rewardMessage,
        QteScoreSummary? scoreSummary)
    {
        var rank = ending.GrantsReward ? scoreSummary?.Rank?.Label : ending.DisplayName;
        var rankText = string.IsNullOrWhiteSpace(rank) ? ending.DisplayName : rank;
        return $"{ending.DisplayName}: {ending.Summary} {ending.Epilogue} Счёт вылазки {ending.NormalizedScore}/100. Ранг: {rankText}. {rewardMessage}";
    }

    private static QteScoreSummary? BuildDarenFinalScoreSummary(
        QteScoreModel? scoreModel,
        QteScoreState? scoreState,
        DarenEndingResult ending)
    {
        var summary = BuildFinalScoreSummary(scoreModel, scoreState);
        if (summary != null && !ending.GrantsReward)
        {
            summary.Rank = new QteScoreRankSummary
            {
                Id = ending.OutcomeId,
                Label = ending.DisplayName,
                Summary = ending.Summary
            };
        }

        return summary;
    }

    private static void RenderDarenCompletion(DarenShowcaseAttemptState attempt, QteSceneCompletion completion)
    {
        var lines = new List<string>
        {
            completion.Response.Response ?? completion.Summary,
            "",
            DarenBoundaryNotice,
            DarenRewardNotice
        };
        if (attempt.Ending is { } ending)
        {
            lines.Add($"Итог: {ending.DisplayName}, счёт {ending.NormalizedScore}/100.");
        }

        AnsiConsole.Write(new Panel(new Markup(Markup.Escape(string.Join("\n", lines))))
        {
            Header = new PanelHeader(" Итог вылазки Дарена "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        });
    }

    public sealed class DarenShowcaseRouteDefinition
    {
        public string RouteId { get; init; } = DarenRouteId;
        public IReadOnlyList<DarenShowcaseBeat> Beats { get; init; } = [];
        public QteOffer Offer { get; init; } = new();
        public IReadOnlyList<DarenEndingTier> EndingTiers { get; init; } = [];
    }

    public sealed record DarenShowcaseBeat(string BeatId, string Title, string PlayerText);

    public sealed class DarenShowcaseAttemptState
    {
        public string AttemptId { get; init; } = "";
        public string State { get; set; } = "Active";
        public ActiveQteSceneState ActiveScene { get; init; } = new();
        public QteActionResolution? LastResolution { get; set; }
        public QteSceneCompletion? LastCompletion { get; set; }
        public DarenShowcaseEnding? Ending { get; set; }
        public bool HadUnsafeRouteFailure { get; set; }
        public string FeedbackTitle { get; set; } = "";
        public string Feedback { get; set; } = "";
        public string BoundaryNotice { get; init; } = DarenBoundaryNotice;
        public string RewardNotice { get; init; } = DarenRewardNotice;
    }

    public sealed record DarenShowcaseEnding(
        string? TierId,
        string DisplayName,
        int NormalizedScore,
        int InkFeatherBonus,
        bool GrantsReward,
        string Epilogue,
        string RewardExplanation,
        string RewardMessage);

    private const string DarenBoundaryNotice =
        "Это отдельная авторская вылазка: обычная глава, обычные ходы и свободная тренировка испытаний не меняются.";

    private const string DarenRewardNotice =
        "Лучший итог Дарена запоминается книгой и даёт Чернильные Перья только при создании будущей новой игры.";
}
