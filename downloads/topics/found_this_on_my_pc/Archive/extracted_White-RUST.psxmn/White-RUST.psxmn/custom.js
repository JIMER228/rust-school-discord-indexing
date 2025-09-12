var currentUrl = window.location.href;

//if (currentUrl.startsWith("https://whitewhite2.gamestores.app")) {

    // Setting's shop
    const event = new CustomEvent("setCustomConfig")

    window.productsGrid = 4;
    window.currency = '₽';
    window.defaultPaymentAmount = 150;
    window.zeroToFree = true;
    hideServerSelector = true;
    hideTotalOnline = true;
    sidebarStoreToRight = true;

    window.dispatchEvent(event);

    if (window.innerWidth > 670) {
        function shopHeader() {
            window.dispatchEvent(new CustomEvent("initState"));
            window.dispatchEvent(new CustomEvent("initComponentsManager"));

            window.componentsManager.addListener('HEADER', 'DID_MOUNT', ()=>{
                function addNewHeader() {
                    const htmlCode = `<header class="Header-module__custom">
                                          <div class="HeaderButtons-module">
                                              <a href="#" class="HeaderButtons-module__container Container__promo" onclick="openModal('promoModal')">
                                                  <div class="content__image">
                                                      <img src="https://gspics.org/images/2024/02/16/0btm9m.png" alt="Промокод">
                                                  </div>
                                                  <div class="content__text">
                                                      <p>Промокоды</p>
                                                  </div>
                                              </a>
                                              <a href="#" class="HeaderButtons-module__container Container__rules" onclick="openModal('rulesModal')">
                                                  <div class="content__image">
                                                      <img src="https://gspics.org/images/2024/03/03/0eoHKx.png" alt="Правила">
                                                  </div>
                                                  <div class="content__text">
                                                      <p>Правила</p>
                                                  </div>
                                              </a>
                                              <a href="#" class="HeaderButtons-module__container Container__bonus" onclick="openModal('bonusModal')">
                                                  <div class="content__image">
                                                      <img src="https://gspics.org/images/2024/02/16/0btUSJ.png" alt="Бонусы">
                                                  </div>
                                                  <div class="content__text">
                                                      <p>Техническая поддержка</p>
                                                  </div>
                                              </a>
                                          </div>
                                      </header>`

                    const headerElement = document.getElementsByTagName('header')[0];
                    headerElement.insertAdjacentHTML('beforebegin', htmlCode);

                    const {player} = window.getState().player
                    console.log(player);
                    if (!player)
                        return

                    const userAvatar = `
                        <div class="PlayerMenu-module__avatar">
                            <a href="/profile"> 
                                <img src="${player.avatar}" alt="${player.username}"></img>
                            </a>
                        </div> `

                    const profileLink = document.querySelector('.PlayerMenu-module__profileLink')
                    if (profileLink) {
                        profileLink.outerHTML = userAvatar
                    }

                    const currentURL = new URL(window.location.href);
                    const path = currentURL.pathname;
                    const firstLink = document.querySelector('.HeaderNav-module__wrapper a');

                    if (path === '/' || path === '') {
                        firstLink.classList.add('HeaderNav-module__linkActive');
                    } else {
                        firstLink.classList.remove('HeaderNav-module__linkActive');
                    }
                }

                function editElement() {
                    const loginLink = document.querySelector('.PlayerMenu-module__loginLink');
                    const header = document.querySelector('.Header-module__custom');
                    const playerBalanceBtn = document.querySelector('.PlayerBalance-module__btn');
                    const playerMenuProfileLink = document.querySelector('.PlayerMenu-module__avatar');

                    if (loginLink) {
                        loginLink.innerHTML = `<p>Авторизация</p>`;
                        header.appendChild(loginLink);
                    }

                    if (playerBalanceBtn && playerMenuProfileLink) {
                        const headerLinksContainer = document.createElement('div');
                        headerLinksContainer.classList.add('HeaderLinks-module');
                        headerLinksContainer.appendChild(playerBalanceBtn);
                        headerLinksContainer.appendChild(playerMenuProfileLink);
                        header.appendChild(headerLinksContainer);
                    }

                    var button = document.querySelector(".PlayerMenuMobile-module__profileLink");
                    if (button) {
                        button.classList.remove("PlayerMenuMobile-module__profileLink");
                        button.classList.add("profileLink");
                        button.innerHTML = "Профиль";
                    }
                }

                addNewHeader();
                editElement();
                // Function
            }
            )

            window.componentsManager.load()
        }
    } else {
        function shopHeader() {
            window.dispatchEvent(new CustomEvent("initState"));
            window.dispatchEvent(new CustomEvent("initComponentsManager"));

            window.componentsManager.addListener('HEADER', 'DID_MOUNT', ()=>{

                function editElement() {
                    var button = document.querySelector(".PlayerMenuMobile-module__profileLink");
                    if (button) {
                        button.classList.remove("PlayerMenuMobile-module__profileLink");
                        button.classList.add("profileLink");
                        button.classList.add("PlayerBalance-module__wrapper");
                        button.innerHTML = "Профиль";
                    }
                }

                editElement();
                // Function
            }
            )

            window.componentsManager.load()
        }
    }

    // Component's
    function shopMainPage() {
        window.dispatchEvent(new CustomEvent("initState"));
        window.dispatchEvent(new CustomEvent("initComponentsManager"));

        window.componentsManager.addListener('SHOP_PAGE', 'DID_MOUNT', ()=>{
            function addNewsBlock() {
                const htmlCode = `<div class="container newsContainer">
                                      <div class="News-module__container news__promo">
                                          <p class="title"><span>СКИДКИ</span> НА ТОВАРЫ</p>
                                          <p class="description">Каждый вайп, в нашей группе раздают промокоды на скидки</p>
                                          <a target="_blank" href="https://vk.com/public217259823" class="custom__btn">Узнать промокод</a>
                                      </div>
                                      <div class="News-module__container news__24h">
                                          <p class="title"><span>РАЗБЛОКИРОВАТЬ</span> АККАУНТ - ЛЕГКО</p>
                                          <p class="description"> Вы можете подать заявку на разбан, если считаете, что ваша блокировка необоснованна</p>
                                          <a href="https://discord.gg/aJBEVW5dKS" class="custom__btn">Оставить заявку</a>
                                      </div>
                                  </div>`;

                const mainElement = document.querySelector("main");
                const existingElement = mainElement.querySelector(".newsContainer");

                if (!existingElement) {
                    mainElement.insertAdjacentHTML("beforebegin", htmlCode);
                }
            }

            function editElement() {
                const categoriesContainer = document.querySelector('.Categories-module__categories');
                const serversModule = document.querySelector('.Servers-module__servers');
                const searchWrapper = document.querySelector('.Search-module__wrapper');

                if (categoriesContainer && serversModule && searchWrapper) {
                    const modulesContainer = document.createElement('div');
                    modulesContainer.classList.add('Modules-container');

                    categoriesContainer.parentNode.insertBefore(modulesContainer, categoriesContainer);

                    modulesContainer.appendChild(serversModule);
                    modulesContainer.appendChild(searchWrapper);
                }

            }

            function addElement() {
                const spanElement = document.querySelector('.Product-module__price > span:not([class])');

                if (spanElement) {
                    const spanElements = document.querySelectorAll('.Product-module__price > span:not([class])');

                    spanElements.forEach(spanElement=>{
                        spanElement.classList.add('span__price');

                        const imgElement = document.createElement('img');
                        imgElement.src = 'https://gspics.org/images/2024/02/18/0b3QQN.png';
                        imgElement.alt = 'Корзина';

                        spanElement.prepend(imgElement);
                    }
                    );
                }
            }

            function addModalPromo() {
                const htmlCode = `<div role="presentation" onmousedown="document.querySelector('#promoModal').classList.remove('active');" id="promoModal" class="customModalWrapper">
                                  <div class="customModalOverflow">
                                     <div class="customModalPosition">
                                        <div role="presentation" onmousedown="event.stopPropagation();" class="customModalContent">
                                           <div class="boxHeader">
                                              <div class="text">
                                                  <p class="title">Промокоды</p>
                                                  <img src="https://gspics.org/images/2024/02/17/0bng7u.png" alt="Промокоды">
                                              </div>
                                              <button onclick="document.querySelector('#promoModal').classList.remove('active');" type="button" class="btn Button-module__btn Button-module__gray">
                                                  <img src="https://gspics.org/images/2024/02/17/0bnMuo.png" alt="Закрыть">
                                              </button>
                                           </div>
                                           <div class="boxBody">
                                              <div class="body__content">
                                                  <p>Все промокоды активируются в <span><a href="/profile">личном профиле</span></a>.<br> Всего их существует два вида:<br>
                                                  <br>
                                                  <span>На баланс</span> - это промокоды, после введения которого ваш донат счёт увеличивается на n-ое количество.<br>
                                                  <br>
                                                  <span>На скидку</span> - это промокоды, после введения которых у вас появляется персональная скидка на все товары.
                                              </div>
                                           </div>
                                        </div>
                                     </div>
                                  </div>
                               </div>`

                document.querySelector('main').insertAdjacentHTML('beforeend', htmlCode);
            }

            function addModalBonus() {
                const htmlCode = `<div role="presentation" onmousedown="document.querySelector('#bonusModal').classList.remove('active');" id="bonusModal" class="customModalWrapper">
                                  <div class="customModalOverflow">
                                     <div class="customModalPosition">
                                        <div role="presentation" onmousedown="event.stopPropagation();" class="customModalContent">
                                           <div class="boxHeader">
                                              <div class="text">
                                                  <p class="title">Техническая поддержка</p>
                                                  <img src="https://gspics.org/images/2024/02/18/0bny7X.png" alt="Бонусы">
                                              </div>
                                              <button onclick="document.querySelector('#bonusModal').classList.remove('active');" type="button" class="btn Button-module__btn Button-module__gray">
                                                  <img src="https://gspics.org/images/2024/02/17/0bnMuo.png" alt="Закрыть">
                                              </button>
                                           </div>
                                           <div class="boxBody boxCustom">
												<p>По поводу проблем с магазином обращайтесь в личные сообщения Discord по данным контактам:<br><br>imsfr,<br> zona00,<br> links531</p>
                                           </div>
                                        </div>
                                     </div>
                                  </div>
                               </div>`

                document.querySelector('main').insertAdjacentHTML('beforeend', htmlCode);
            }

            function addModalRules() {
                const htmlCode = `<div role="presentation" onmousedown="document.querySelector('#rulesModal').classList.remove('active');" id="rulesModal" class="customModalWrapper">
                                  <div class="customModalOverflow">
                                     <div class="customModalPosition">
                                        <div role="presentation" onmousedown="event.stopPropagation();" class="customModalContent">
                                           <div class="boxHeader">
                                              <div class="text">
                                                  <p class="title">Правила проекта</p>
                                                  <img src="https://gspics.org/images/2024/03/03/0eoQ1w.png" alt="Бонусы">
                                              </div>
                                              <button onclick="document.querySelector('#rulesModal').classList.remove('active');" type="button" class="btn Button-module__btn Button-module__gray">
                                                  <img src="https://gspics.org/images/2024/02/17/0bnMuo.png" alt="Закрыть">
                                              </button>
                                           </div>
                                           <div class="boxBody boxCustom" style="display: flex; flex-direction: column;">
												<p>1.1. Общие правила проекта.<br>
                                                1.1 Находясь на игровом сервере White rust, в группе ВК или Discord Вы подтверждаете, что ознакомились и согласились соблюдать правила проекта.<br>
                                                1.2 Незнание правил проекта не освобождает игрока от ответственности.<br>
                                                1.3 Вы и только вы несёте ответственность за все свои/находящиеся на пк аккаунты, файлы, истории и поступки.<br>
                                                1.4 Любые вида правил могут меняться в любое время суток, администрация вправе оповестить или не оповестить о изменениях.<br>
                                                1.5 Модерация и администрация сами выбирают наказание для игрока в зависимости от степени нарушения и обстоятельств. Игрок может отделаться простым предупреждением, а может и попасть в бессрочный бан.<br>
                                                1.6 Администрация вправе не возвращать ресурсы, утраченные во время игрового процесса/рестартов/багов и т.п.<br>
                                                1.7 Шутки/вопросы/просьбы вроде:<br>
                                                "Дай ресы", "Админ блатит", "Скажи мне где он живет", "Не говори так, а то сейчас за просто так забанит", "Админ мне помог дом зарейдить/построить/просветить", "Админам похуй на сервер", "Я друг админа/модератора и он тебя сейчас забанит (за просто так)" <br>
                                                И все в подобном роде - администрацией проекта расценивается, как грубейшее оскорбление в сторону проекта. Бан до 1 месяца<br>
                                                1.8 Если Вы заметили играющих c посторонним ПО, отправьте жалобу командой /report<br>
                                                1.9 Вы несёте ответственность за все свои аккаунты. Получив бан на старом аккаунте, вы получите его и на новом.</p>

                                                <p>Запрещенные по/баги<br>
                                                2.1 Запрещено использование возможностей читеров. Если вы знаете/имеете доказательства на игрока использующего запрещенное ПО но умалчиваете это - вы получите такую же блокировку как и он. Бессрочный бан<br>
                                                2.2 Запрещается использовать/хранить/распространять/иметь подписку/историю покупки или наличие любого софта, дающего преимущество в игре. Хранением также будет признаваться, если сторонний софт будет найден на облачных серверах хранения (Яндекс диск, Гугл диск и т.д.), а также в других местах, к которым игрок имеет доступ Бессрочный бан<br>
                                                2.3 Запрещено хранение/покупка запрещённого программного обеспечения любого срока давности скачивания/на любой версии RUST/любого формата. Бессрочный бан<br>
                                                2.4 Запрещено хранение остаточных файлов после использования любого вида запрещённого программного обеспечения, любого срока давности Бессрочный бан<br>
                                                2.5 Запрещена покупка запрещённого программного обеспечения, даже если само программное обеспечение отсутствует на ПК подозреваемого. Любое подтверждение действующей лицензии ПО/подтверждение покупки/скачивания любого вида запрещённого программного обеспечения может послужить причиной для блокировки Бессрочный бан<br>
                                                2.6 Запрещена чистка ПК перед проверкой (В случае видимых доказательств, что ПК - был почищен, администрация / модерация вправе остановить проверку и заблокировать вас) Бессрочный бан<br>
                                                2.7 Блокировку можно получить из признания игрока в использовании запрещённого ПО в любом из чатов. Администрация не будет это расценивать как «сарказм» Бессрочный бан<br>
                                                2.8 Запрещено использование/распространение серверных/игровых багов и недоработок. Бан до 3 дней<br>
                                                2.9 При обнаружении любого бага/любой недоработки сервера/карты/плагинов, незамедлительно сообщите об этом администрации проекта. Умалчивание будет приравниваться к использованию. Бан до 3 дней<br>
                                                2.10 Если у вашего тиммейта нашли запрещенное ПО, то, модератор вправе вызвать вас на проверку, без каких-либо объяснений.<br>
                                                2.11 Запрещены сайты, с содержанием ссылки на скачивание/приобретением стороннего программного обеспечения. В больших кол-вах, в течении последних 3х месяцев. Предупреждение ½<br>
                                                2.12 Разрешено использовать программное обеспечение, которое не облегчает игровой процесс механическим путём (не встраивается непосредственно в саму игру). Например: программы, позволяющие "нарисовать" прицел в центре экрана.</p>

                                                <p>Лимит игроков<br>
                                                3.1 Запрещено превышать лимит игроков в команде, установленный сервером.(х2 – мах 4 / Х3 – мах 2 ) Вплоть до перманентного бана<br>
                                                3.2 Запрещена смена команды/смена напарника на время, если ваша основная команда/основной напарник продолжит с вами играть. Вплоть до перманентного бана<br>
                                                3.3 Запрещена частая смена команды/смена напарника. (За один вайп – не более 3 раз) Вплоть до перманентного бана<br>
                                                3.4 Запрещены любые перемирия/союзы/мирные соглашения с соседями, если в сумме превышается лимит игроков (4+ / 2+) (Не зависимо от того мешаете ли вы другим игрокам или нет) Вплоть до перманентного бана<br>
                                                3.5 Запрещено помогать кому-либо в рейдах/антирейдах, если в сумме превышается лимит (4+ / 2+), установленный сервером и нарушает другие правила лимита (считаются онлайн и оффлайн игроки всех команд, даже если они не задействованы рейдах/антирейдах). Вплоть до перманентного бана<br>
                                                3.6 Разрешено сменить напарника/тиммейта/команду, если ваши предыдущие напарник/тиммейт/команда перестали играть, либо не будут в дальнейшем контактировать с вами.</p>

                                                <p>Правила игрового чата<br>
                                                4.1 Запрещено проявление расизма/национализма/нацизма в любой форме. Бан до месяца<br>
                                                4.2 Запрещено оскорбление родителей/национальности/государств других игроков. Администрация вправе сама выбрать, ограничитесь вы мутом или временным баном Бан до месяца<br>
                                                4.3 Разрешены оскорбления игроков в пределах разумного (нецензурная брань в умеренном количестве) [Если, это не переходит рамки адекватности]<br>
                                                4.4 Запрещено чересчур токсичное поведение (использование нецензурной брани и оскорбительных выражений в очень большом количестве).​ Бан до месяца<br>
                                                4.5 Запрещены оскорбления проекта в любой форме. Бан до перманента<br>
                                                4.6 Запрещено выдавать себя за модератора/администратора проекта. Запрещается в ник вставлять должности администрации проекта. Пример: [ADMIN] Бан до месяца<br>
                                                4.7 Запрещается реклама каких-либо других проектов, не имеющих отношения к нашим. Бан до месяца<br>
                                                4.8 Запрещено продавать / обсуждать, запрещённое ПО Бан до месяца<br>
                                                4.9 Запрещается клевета, дезинформация на игроков/администрацию и проект в целом. ​ Бан до месяца<br>
                                                4.10 Запрещается флуд. Флудом считается отправка нескольких одинаковых сообщений и/или команд в короткий промежуток времени. Бан до 3х дней<br>
                                                4.11 Запрещено токсичное поведение, неуважение или оскорбление администрации во всех проявлениях. Бан до перманента</p>

                                                <p>Правила дискорд сервера.<br>
                                                5.1 Запрещено копировать профили, а также ставить оскорбительные или провокационные никнеймы/статусы/аватары и выдавать себя за другую личность. Бан до месяца<br>
                                                5.2 Запрещено предлагать покупку/продажу/обмен, а также выпрашивать любые материальные/цифровые ценности и услуги. ᅠБан до перманента<br>
                                                5.3 Запрещено оскорблять, унижать, провоцировать, обманывать, шантажировать участников/администрацию или их близких. ᅠБан до перманента<br>
                                                5.4 Запрещено неадекватное поведение в любых проявлениях. ᅠБан до перманента<br>
                                                5.5 Запрещено распространять личную информацию без согласия её владельца. Бан до перманента<br>
                                                5.6 Запрещено разжигать национальную розню, дискриминацию, конфликты на политической почве, враждебность к любым религиозным группам и людям с ограниченными возможностями. ᅠБан до перманента<br>
                                                5.7 Запрещено использование другой учётной записи, для избежание наложенных наказаний. Бан до перманента<br>
                                                5.8 Запрещена реклама сторонних проектов, в любом ее проявлении. ᅠБан до перманента<br>
                                                5.9 Запрещены деструктивные действия по отношению к серверу: неконструктивная критика администрации, призывы покинуть сервер или любые способы, способные привести к помехам в процессе развития сервера. Бан до перманента<br>
                                                5.10 Запрещено беспричинно или слишком часто упоминать роли и профили участников и администрации. ᅠБан до перманента<br>
                                                5.11 Запрещено издавать громкие или мешающие звуки, препятствовать общению. Бан до 3 дней</p>

                                                <p>Правила проверки<br>
                                                6.1 Проверки проходят только через программу «DISCORD». Каждый игрок на нашем проекте, должен иметь данную программу на своём пк и быть в ней зарегистрированным.<br>
                                                6.2 Запрещено игнорирование вызова на проверку Бан до перманента<br>
                                                6.3 Запрещено выходить из игры после вызова на проверку. В случае, если у подозреваемого слабый компьютер, и его совершенно случайно выкинуло с сервера, на повторное подключение даётся 10 минут. Бан до перманента<br>
                                                6.4 Модерации и администрации не интересна ваша личная информация. Все аспекты проверки направлены сугубо на выявление запрещённого программного обеспечения.<br>
                                                6.5 Запрещено покидать проверку по каким-либо причинам, если модератор сам не завершил проверку Бан до перманента<br>
                                                6.6 Вы имеете полное право отказаться проходить проверку, но в этом случае вы будете забанены. Бан до перманента<br>
                                                6.7 Соглашаясь пройти проверку вы разрешаете устанавливать сторонние программы нужные модератору для проверки вашего PC на наличие запрещённых Программных Обеспечений. ( Everything, Lastactivity, USB Deview, Oz protect, any desk process hacker ) В случае отказа, игрок будет заблокирован на проекте. Бан до перманента<br>
                                                6.8 Оспаривание бана или другого наказания осуществляется в течение 5х дней и только тем игроком, к которому это наказание применено.<br>
                                                6.9 Вас могут вызвать на проверку лишь в том случае, если вы не AFK и не имеете рейд-блока.<br>
                                                6.10 При вызове на проверку, подозреваемому выделено 5 минут на предоставление контактной информации - своего Discord-имени.<br>
                                                6.11 На проверке недопустимо присутствие посторонних людей. Проверка может проходить только один на один с модератором/администратором, либо с несколькими модераторами и администратором.<br>
                                                6.12. Запрещены оскорбления/крайне токсичное поведение на проверке в отношении модерации/администрации проекта. Бан до перманента<br>
                                                6.13 При проверке на макросы вас могут попросить включить вебку с пк или телефона, для визуального контроля обращения с мышкой. (Также могут попросить сделать какое-то действие в игре)<br>
                                                6.14 На сервере не существует банов за просто так. (Если же вы уверены, что вам был выдан бан ошибочно, вы имеете право оспаривать решения модератора / администратора.<br>
                                                6.15 При отправке своего Discrod модератору, проверьте, правильно ли вы его вписали (В случае ошибочно указанного дискорда, это - ваша вина)<br>
                                                6.16 Запрещен обход проверок в любом виде. Бан до перманента</p>
                                           </div>
                                        </div>
                                     </div>
                                  </div>
                               </div>`

                document.querySelector('main').insertAdjacentHTML('beforeend', htmlCode);
            }

            addNewsBlock();
            editElement();
            addElement();
            addModalPromo();
            addModalBonus();
            addModalRules();
            // Function
        }
        )

        window.componentsManager.load()
    }

    function shopProfilePage() {
        window.dispatchEvent(new CustomEvent("initState"));
        window.dispatchEvent(new CustomEvent("initComponentsManager"));

        window.componentsManager.addListener('PROFILE_PAGE', 'DID_MOUNT', ()=>{

            function addModalPromo() {
                const htmlCode = `<div role="presentation" onmousedown="document.querySelector('#promoModal').classList.remove('active');" id="promoModal" class="customModalWrapper">
                                  <div class="customModalOverflow">
                                     <div class="customModalPosition">
                                        <div role="presentation" onmousedown="event.stopPropagation();" class="customModalContent">
                                           <div class="boxHeader">
                                              <div class="text">
                                                  <p class="title">Промокоды</p>
                                                  <img src="https://gspics.org/images/2024/02/17/0bng7u.png" alt="Промокоды">
                                              </div>
                                              <button onclick="document.querySelector('#promoModal').classList.remove('active');" type="button" class="btn Button-module__btn Button-module__gray">
                                                  <img src="https://gspics.org/images/2024/02/17/0bnMuo.png" alt="Закрыть">
                                              </button>
                                           </div>
                                           <div class="boxBody">
                                              <div class="body__content">
                                                  <p>Все промокоды активируются в <span><a href="/profile">личном профиле</span></a>.<br> Всего их существует два вида:<br>
                                                  <br>
                                                  <span>На баланс</span> - это промокоды, после введения которого ваш донат счёт увеличивается на n-ое количество.<br>
                                                  <br>
                                                  <span>На скидку</span> - это промокоды, после введения которых у вас появляется персональная скидка на все товары.
                                              </div>
                                           </div>
                                        </div>
                                     </div>
                                  </div>
                               </div>`

                document.querySelector('main').insertAdjacentHTML('beforeend', htmlCode);
            }

            function addModalBonus() {
                const htmlCode = `<div role="presentation" onmousedown="document.querySelector('#bonusModal').classList.remove('active');" id="bonusModal" class="customModalWrapper">
                                  <div class="customModalOverflow">
                                     <div class="customModalPosition">
                                        <div role="presentation" onmousedown="event.stopPropagation();" class="customModalContent">
                                           <div class="boxHeader">
                                              <div class="text">
                                                  <p class="title">Техническая поддержка</p>
                                                  <img src="https://gspics.org/images/2024/02/18/0bny7X.png" alt="Бонусы">
                                              </div>
                                              <button onclick="document.querySelector('#bonusModal').classList.remove('active');" type="button" class="btn Button-module__btn Button-module__gray">
                                                  <img src="https://gspics.org/images/2024/02/17/0bnMuo.png" alt="Закрыть">
                                              </button>
                                           </div>
                                           <div class="boxBody boxCustom">
												<p>По поводу проблем с магазином обращайтесь в личные сообщения Discord по данным контактам:<br><br>imsfr,<br> zona00,<br> links531</p>
                                           </div>
                                        </div>
                                     </div>
                                  </div>
                               </div>`

                document.querySelector('main').insertAdjacentHTML('beforeend', htmlCode);
            }

            function addModalRules() {
                const htmlCode = `<div role="presentation" onmousedown="document.querySelector('#rulesModal').classList.remove('active');" id="rulesModal" class="customModalWrapper">
                                  <div class="customModalOverflow">
                                     <div class="customModalPosition">
                                        <div role="presentation" onmousedown="event.stopPropagation();" class="customModalContent">
                                           <div class="boxHeader">
                                              <div class="text">
                                                  <p class="title">Правила проекта</p>
                                                  <img src="https://gspics.org/images/2024/03/03/0eoQ1w.png" alt="Бонусы">
                                              </div>
                                              <button onclick="document.querySelector('#rulesModal').classList.remove('active');" type="button" class="btn Button-module__btn Button-module__gray">
                                                  <img src="https://gspics.org/images/2024/02/17/0bnMuo.png" alt="Закрыть">
                                              </button>
                                           </div>
                                           <div class="boxBody boxCustom" style="display: flex; flex-direction: column;">
												<p>1.1. Общие правила проекта.<br>
                                                1.1 Находясь на игровом сервере White rust, в группе ВК или Discord Вы подтверждаете, что ознакомились и согласились соблюдать правила проекта.<br>
                                                1.2 Незнание правил проекта не освобождает игрока от ответственности.<br>
                                                1.3 Вы и только вы несёте ответственность за все свои/находящиеся на пк аккаунты, файлы, истории и поступки.<br>
                                                1.4 Любые вида правил могут меняться в любое время суток, администрация вправе оповестить или не оповестить о изменениях.<br>
                                                1.5 Модерация и администрация сами выбирают наказание для игрока в зависимости от степени нарушения и обстоятельств. Игрок может отделаться простым предупреждением, а может и попасть в бессрочный бан.<br>
                                                1.6 Администрация вправе не возвращать ресурсы, утраченные во время игрового процесса/рестартов/багов и т.п.<br>
                                                1.7 Шутки/вопросы/просьбы вроде:<br>
                                                "Дай ресы", "Админ блатит", "Скажи мне где он живет", "Не говори так, а то сейчас за просто так забанит", "Админ мне помог дом зарейдить/построить/просветить", "Админам похуй на сервер", "Я друг админа/модератора и он тебя сейчас забанит (за просто так)" <br>
                                                И все в подобном роде - администрацией проекта расценивается, как грубейшее оскорбление в сторону проекта. Бан до 1 месяца<br>
                                                1.8 Если Вы заметили играющих c посторонним ПО, отправьте жалобу командой /report<br>
                                                1.9 Вы несёте ответственность за все свои аккаунты. Получив бан на старом аккаунте, вы получите его и на новом.</p>

                                                <p>Запрещенные по/баги<br>
                                                2.1 Запрещено использование возможностей читеров. Если вы знаете/имеете доказательства на игрока использующего запрещенное ПО но умалчиваете это - вы получите такую же блокировку как и он. Бессрочный бан<br>
                                                2.2 Запрещается использовать/хранить/распространять/иметь подписку/историю покупки или наличие любого софта, дающего преимущество в игре. Хранением также будет признаваться, если сторонний софт будет найден на облачных серверах хранения (Яндекс диск, Гугл диск и т.д.), а также в других местах, к которым игрок имеет доступ Бессрочный бан<br>
                                                2.3 Запрещено хранение/покупка запрещённого программного обеспечения любого срока давности скачивания/на любой версии RUST/любого формата. Бессрочный бан<br>
                                                2.4 Запрещено хранение остаточных файлов после использования любого вида запрещённого программного обеспечения, любого срока давности Бессрочный бан<br>
                                                2.5 Запрещена покупка запрещённого программного обеспечения, даже если само программное обеспечение отсутствует на ПК подозреваемого. Любое подтверждение действующей лицензии ПО/подтверждение покупки/скачивания любого вида запрещённого программного обеспечения может послужить причиной для блокировки Бессрочный бан<br>
                                                2.6 Запрещена чистка ПК перед проверкой (В случае видимых доказательств, что ПК - был почищен, администрация / модерация вправе остановить проверку и заблокировать вас) Бессрочный бан<br>
                                                2.7 Блокировку можно получить из признания игрока в использовании запрещённого ПО в любом из чатов. Администрация не будет это расценивать как «сарказм» Бессрочный бан<br>
                                                2.8 Запрещено использование/распространение серверных/игровых багов и недоработок. Бан до 3 дней<br>
                                                2.9 При обнаружении любого бага/любой недоработки сервера/карты/плагинов, незамедлительно сообщите об этом администрации проекта. Умалчивание будет приравниваться к использованию. Бан до 3 дней<br>
                                                2.10 Если у вашего тиммейта нашли запрещенное ПО, то, модератор вправе вызвать вас на проверку, без каких-либо объяснений.<br>
                                                2.11 Запрещены сайты, с содержанием ссылки на скачивание/приобретением стороннего программного обеспечения. В больших кол-вах, в течении последних 3х месяцев. Предупреждение ½<br>
                                                2.12 Разрешено использовать программное обеспечение, которое не облегчает игровой процесс механическим путём (не встраивается непосредственно в саму игру). Например: программы, позволяющие "нарисовать" прицел в центре экрана.</p>

                                                <p>Лимит игроков<br>
                                                3.1 Запрещено превышать лимит игроков в команде, установленный сервером.(х2 – мах 4 / Х3 – мах 2 ) Вплоть до перманентного бана<br>
                                                3.2 Запрещена смена команды/смена напарника на время, если ваша основная команда/основной напарник продолжит с вами играть. Вплоть до перманентного бана<br>
                                                3.3 Запрещена частая смена команды/смена напарника. (За один вайп – не более 3 раз) Вплоть до перманентного бана<br>
                                                3.4 Запрещены любые перемирия/союзы/мирные соглашения с соседями, если в сумме превышается лимит игроков (4+ / 2+) (Не зависимо от того мешаете ли вы другим игрокам или нет) Вплоть до перманентного бана<br>
                                                3.5 Запрещено помогать кому-либо в рейдах/антирейдах, если в сумме превышается лимит (4+ / 2+), установленный сервером и нарушает другие правила лимита (считаются онлайн и оффлайн игроки всех команд, даже если они не задействованы рейдах/антирейдах). Вплоть до перманентного бана<br>
                                                3.6 Разрешено сменить напарника/тиммейта/команду, если ваши предыдущие напарник/тиммейт/команда перестали играть, либо не будут в дальнейшем контактировать с вами.</p>

                                                <p>Правила игрового чата<br>
                                                4.1 Запрещено проявление расизма/национализма/нацизма в любой форме. Бан до месяца<br>
                                                4.2 Запрещено оскорбление родителей/национальности/государств других игроков. Администрация вправе сама выбрать, ограничитесь вы мутом или временным баном Бан до месяца<br>
                                                4.3 Разрешены оскорбления игроков в пределах разумного (нецензурная брань в умеренном количестве) [Если, это не переходит рамки адекватности]<br>
                                                4.4 Запрещено чересчур токсичное поведение (использование нецензурной брани и оскорбительных выражений в очень большом количестве).​ Бан до месяца<br>
                                                4.5 Запрещены оскорбления проекта в любой форме. Бан до перманента<br>
                                                4.6 Запрещено выдавать себя за модератора/администратора проекта. Запрещается в ник вставлять должности администрации проекта. Пример: [ADMIN] Бан до месяца<br>
                                                4.7 Запрещается реклама каких-либо других проектов, не имеющих отношения к нашим. Бан до месяца<br>
                                                4.8 Запрещено продавать / обсуждать, запрещённое ПО Бан до месяца<br>
                                                4.9 Запрещается клевета, дезинформация на игроков/администрацию и проект в целом. ​ Бан до месяца<br>
                                                4.10 Запрещается флуд. Флудом считается отправка нескольких одинаковых сообщений и/или команд в короткий промежуток времени. Бан до 3х дней<br>
                                                4.11 Запрещено токсичное поведение, неуважение или оскорбление администрации во всех проявлениях. Бан до перманента</p>

                                                <p>Правила дискорд сервера.<br>
                                                5.1 Запрещено копировать профили, а также ставить оскорбительные или провокационные никнеймы/статусы/аватары и выдавать себя за другую личность. Бан до месяца<br>
                                                5.2 Запрещено предлагать покупку/продажу/обмен, а также выпрашивать любые материальные/цифровые ценности и услуги. ᅠБан до перманента<br>
                                                5.3 Запрещено оскорблять, унижать, провоцировать, обманывать, шантажировать участников/администрацию или их близких. ᅠБан до перманента<br>
                                                5.4 Запрещено неадекватное поведение в любых проявлениях. ᅠБан до перманента<br>
                                                5.5 Запрещено распространять личную информацию без согласия её владельца. Бан до перманента<br>
                                                5.6 Запрещено разжигать национальную розню, дискриминацию, конфликты на политической почве, враждебность к любым религиозным группам и людям с ограниченными возможностями. ᅠБан до перманента<br>
                                                5.7 Запрещено использование другой учётной записи, для избежание наложенных наказаний. Бан до перманента<br>
                                                5.8 Запрещена реклама сторонних проектов, в любом ее проявлении. ᅠБан до перманента<br>
                                                5.9 Запрещены деструктивные действия по отношению к серверу: неконструктивная критика администрации, призывы покинуть сервер или любые способы, способные привести к помехам в процессе развития сервера. Бан до перманента<br>
                                                5.10 Запрещено беспричинно или слишком часто упоминать роли и профили участников и администрации. ᅠБан до перманента<br>
                                                5.11 Запрещено издавать громкие или мешающие звуки, препятствовать общению. Бан до 3 дней</p>

                                                <p>Правила проверки<br>
                                                6.1 Проверки проходят только через программу «DISCORD». Каждый игрок на нашем проекте, должен иметь данную программу на своём пк и быть в ней зарегистрированным.<br>
                                                6.2 Запрещено игнорирование вызова на проверку Бан до перманента<br>
                                                6.3 Запрещено выходить из игры после вызова на проверку. В случае, если у подозреваемого слабый компьютер, и его совершенно случайно выкинуло с сервера, на повторное подключение даётся 10 минут. Бан до перманента<br>
                                                6.4 Модерации и администрации не интересна ваша личная информация. Все аспекты проверки направлены сугубо на выявление запрещённого программного обеспечения.<br>
                                                6.5 Запрещено покидать проверку по каким-либо причинам, если модератор сам не завершил проверку Бан до перманента<br>
                                                6.6 Вы имеете полное право отказаться проходить проверку, но в этом случае вы будете забанены. Бан до перманента<br>
                                                6.7 Соглашаясь пройти проверку вы разрешаете устанавливать сторонние программы нужные модератору для проверки вашего PC на наличие запрещённых Программных Обеспечений. ( Everything, Lastactivity, USB Deview, Oz protect, any desk process hacker ) В случае отказа, игрок будет заблокирован на проекте. Бан до перманента<br>
                                                6.8 Оспаривание бана или другого наказания осуществляется в течение 5х дней и только тем игроком, к которому это наказание применено.<br>
                                                6.9 Вас могут вызвать на проверку лишь в том случае, если вы не AFK и не имеете рейд-блока.<br>
                                                6.10 При вызове на проверку, подозреваемому выделено 5 минут на предоставление контактной информации - своего Discord-имени.<br>
                                                6.11 На проверке недопустимо присутствие посторонних людей. Проверка может проходить только один на один с модератором/администратором, либо с несколькими модераторами и администратором.<br>
                                                6.12. Запрещены оскорбления/крайне токсичное поведение на проверке в отношении модерации/администрации проекта. Бан до перманента<br>
                                                6.13 При проверке на макросы вас могут попросить включить вебку с пк или телефона, для визуального контроля обращения с мышкой. (Также могут попросить сделать какое-то действие в игре)<br>
                                                6.14 На сервере не существует банов за просто так. (Если же вы уверены, что вам был выдан бан ошибочно, вы имеете право оспаривать решения модератора / администратора.<br>
                                                6.15 При отправке своего Discrod модератору, проверьте, правильно ли вы его вписали (В случае ошибочно указанного дискорда, это - ваша вина)<br>
                                                6.16 Запрещен обход проверок в любом виде. Бан до перманента</p>
                                           </div>
                                        </div>
                                     </div>
                                  </div>
                               </div>`

                document.querySelector('main').insertAdjacentHTML('beforeend', htmlCode);
            }

            function addElement() {
                const container = document.querySelector('.ProfileNav-module__body.boxBody');
                const buttons = container.querySelectorAll('a');

                buttons.forEach((button)=>{
                    const image = document.createElement('img');
                    const buttonText = button.innerText;

                    let imagePath = '';
                    switch (buttonText) {
                    case 'Профиль':
                        imagePath = 'https://gspics.org/images/2024/02/18/0b6Mux.png';
                        break;
                    case 'Корзина':
                        imagePath = 'https://gspics.org/images/2024/02/18/0b6g7Q.png';
                        break;
                    case 'История':
                        imagePath = 'https://gspics.org/images/2024/02/18/0b6X6a.png';
                        break;
                    default:
                        imagePath = null;
                    }

                    image.src = imagePath;
                    image.alt = buttonText;

                    button.insertBefore(image, button.firstChild);
                }
                );
            }

            function editElement() {
                const logoutButton = document.querySelector('.ProfileNav-module__logOut');
                if (logoutButton) {
                    logoutButton.textContent = 'Выйти из аккаунта';
                    logoutButton.addEventListener('click', function() {
                        location.reload();
                    });
                }
            }

            addModalPromo();
            addModalBonus();
            addModalRules();
            addElement();
            editElement();
            // Function
        }
        )

        window.componentsManager.load()
    }

    // Open modal window
    function openModal(nameModal) {
        var modal = document.getElementById(nameModal);
        if (modal.classList.contains('active')) {
            modal.classList.remove('active');
        } else {
            modal.classList.add('active');
        }
    }

    // Open an image from the product description
    function changeImageSize(index) {
        var images = document.querySelectorAll('.desc__content > a > img');

        if (index >= 0 && index < images.length) {
            var img = images[index];

            if (img.style.width === '385px') {
                img.style.width = '';
                img.style.transition = '';
                img.style.borderRadius = '';
                img.style.zIndex = '';
                img.style.position = '';
                img.style.top = '';
                img.style.left = '';
                img.style.transform = '';
            } else {
                img.style.width = '385px';
                img.style.transition = 'width 0.5s, border-radius 0.5s, z-index 0.5s, position 0.5s, top 0.5s, left 0.5s, transform 0.5s';
                img.style.borderRadius = '8px';
                img.style.zIndex = '999';
                img.style.position = 'absolute';
                img.style.top = '50%';
                img.style.left = '50%';
                img.style.transform = 'translate(-50%, -50%)';
            }
        }
    }

    // Load component's
    if (window.isAppReady) {
        if (typeof shopHeader === 'function') {
            shopHeader();
        }
        shopMainPage();
        shopProfilePage();
    } else {
        window.addEventListener('appReady', ()=>{
            if (typeof shopHeader === 'function') {
                shopHeader();
            }
            shopMainPage();
            shopProfilePage();
        }
        );
    }

//} else {
  //  alert("ERROR: You do not have access to the code of this store!");
    //location.reload();
//}
