<?php

namespace Pterodactyl\Console\Commands\Server;

use DateTimeZone;
use Carbon\Carbon;
use Pterodactyl\Models\Wipe;
use Illuminate\Console\Command;
use Pterodactyl\Jobs\WipeServerJob;
use Pterodactyl\Repositories\Wings\DaemonPowerRepository;
use Pterodactyl\Repositories\Wings\DaemonCommandRepository;

class RustWipeCommand extends Command
{
    /**
     * The name and signature of the console command.
     *
     * @var string
     */
    protected $signature = 'p:server:wipe';

    /**
     * The console command description.
     *
     * @var string
     */
    protected $description = 'Wipes rust servers on schedule.';

    /**
     * Create a new command instance.
     *
     * @return void
     */
    public function __construct(protected DaemonCommandRepository $commandRepository, protected DaemonPowerRepository $powerRepository)
    {
        parent::__construct();
    }

    /**
     * Execute the console command.
     */
    public function handle(): int
    {
        $wipes = Wipe::all();
        foreach($wipes->filter(function ($wipe) { return !$wipe->ran_at || $wipe->repeat || $wipe->force; }) as $wipe) {
            if ($wipe->server) {
                if ($wipe->server->status !== 'suspended') {
                    try {
                        $wipe_time = $wipe->time;

                        if ($wipe->force) {
                            $now = new Carbon('now', new DateTimeZone('Europe/Amsterdam'));
                            $day = Carbon::create($now->year, $now->month, 1, 20, 0, 0, new DateTimeZone('Europe/Amsterdam'));

                            while ($day->dayOfWeek !== Carbon::THURSDAY) {
                                $day->addDay();
                            }

                            if ($day < $now) {
                                $day = $day->addMonthNoOverflow()->day(1);
                                while ($day->dayOfWeek !== Carbon::THURSDAY) {
                                    $day->addDay();
                                }
                            }

                            $wipe_time = isset($wipe_time) ? min($wipe_time, $day) : $day;
                        }

                        $now = new Carbon(Carbon::now(), new DateTimeZone($wipe->server->timezone ?? DateTimeZone::listIdentifiers(DateTimeZone::ALL)[0]));
                        foreach($wipe->commands as $command) {
                            if ($now->copy()->addMinutes($command->time)->startOfMinute()->format('Y-m-d H:i') === Carbon::parse($wipe_time)->startOfMinute()->format('Y-m-d H:i')) {
                                $this->commandRepository->setServer($wipe->server)->send($command->command);
                            }
                        }
                        if ($wipe_time <= $now) {
                            $this->powerRepository->setServer($wipe->server)->send('stop');
                            dispatch(new WipeServerJob($wipe->server, $wipe->toArray()))->delay(Carbon::now()->addMinute());
                            $wipe->update([
                                'ran_at' => Carbon::now(),
                            ]);
                            if ($wipe->repeat) {
                                $wipe->update([
                                    'time' => Carbon::parse($wipe_time)->addWeek(),
                                ]);
                            }
                        }
                    } catch(\Exception) {
                    }
                }
            } else {
                $wipe->delete();
            }
        }

        return 0;
    }
}