<?php

namespace Pterodactyl\Jobs;

use Exception;
use DateTimeZone;
use Carbon\Carbon;
use Illuminate\Support\Arr;
use Illuminate\Bus\Queueable;
use Pterodactyl\Models\Server;
use Pterodactyl\Models\EggVariable;
use Illuminate\Queue\SerializesModels;
use Pterodactyl\Models\ServerVariable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Pterodactyl\Repositories\Wings\DaemonFileRepository;
use Pterodactyl\Repositories\Wings\DaemonPowerRepository;

class WipeServerJob implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    /**
     * Create a new job instance.
     *
     * @return void
     */
    public function __construct(public Server $server, public array $data)
    {
        $this->queue = 'standard';
    }

    /**
     * Execute the job.
     *
     * @return void
     */
    public function handle(DaemonFileRepository $fileRepository, DaemonPowerRepository $powerRepository)
    {
        $now = new Carbon(Carbon::now(), new DateTimeZone($this->server->timezone ?? DateTimeZone::listIdentifiers(DateTimeZone::ALL)[0]));

        $seed = Arr::get($this->data, 'random_seed') || Arr::get($this->data, 'level') || Arr::get($this->data, 'random_level') ? rand(1, 2147483647) : Arr::get($this->data, 'seed');

        ServerVariable::where('server_id', $this->server->id)->where('variable_id', EggVariable::where('egg_id', $this->server->egg_id)->where('env_variable', 'HOSTNAME')->first()->id)->update([
            'variable_value' => str_replace('%DAY%', $now->day, str_replace('%MONTH%', $now->month, Arr::get($this->data, 'name'))),
        ]);

        ServerVariable::where('server_id', $this->server->id)->where('variable_id', EggVariable::where('egg_id', $this->server->egg_id)->where('env_variable', 'DESCRIPTION')->first()->id)->update([
            'variable_value' => str_replace('%DAY%', $now->day, str_replace('%MONTH%', $now->month, Arr::get($this->data, 'description'))),
        ]);

        ServerVariable::where('server_id', $this->server->id)->where('variable_id', EggVariable::where('egg_id', $this->server->egg_id)->where('env_variable', 'WORLD_SIZE')->first()->id)->update([
            'variable_value' => Arr::get($this->data, 'size') ?? 0,
        ]);

        ServerVariable::where('server_id', $this->server->id)->where('variable_id', EggVariable::where('egg_id', $this->server->egg_id)->where('env_variable', 'WORLD_SEED')->first()->id)->update([
            'variable_value' => $seed,
        ]);

        try {
            $settings = $fileRepository->setServer($this->server)->getContent('/server/rust/cfg/server.cfg');
            $items = [];

            foreach (explode("\n", $settings) as $setting) {
                if (str_contains($setting, 'server.seed')) {
                    $variable = explode(' ', $setting);
                    $variable[1] = (string) $seed;

                    $setting = implode(' ', $variable);
                }
                array_push($items, $setting);
            }

            $file = implode("\n", $items);
            $fileRepository->putContent('/server/rust/cfg/server.cfg', $file);
        } catch(Exception) {
            // do nothing if the server.cfg file isn't found
        }

        $level = Arr::get($this->data, 'random_level') ? ($this->server->wipemaps ? $this->server->wipemaps->shuffle()->where('map', '!=', ServerVariable::where('server_id', $this->server->id)->where('variable_id', EggVariable::where('egg_id', $this->server->egg_id)->where('env_variable', 'MAP_URL')->first()->id)->first()->variable_value)->first()->map : '') : Arr::get($this->data, 'level');

        ServerVariable::where('server_id', $this->server->id)->where('variable_id', EggVariable::where('egg_id', $this->server->egg_id)->where('env_variable', 'MAP_URL')->first()->id)->update([
            'variable_value' => $level ?? '',
        ]);

        if (Arr::get($this->data, 'files')) {
            foreach (explode(PHP_EOL, Arr::get($this->data, 'files')) as $file) {
                $directory = substr($file, 0, strrpos($file, '/'));
                $filenamePattern = substr($file, strrpos($file, '/') + 1);

                if (str_contains($filenamePattern, '*')) {
                    $regexPattern = '/^' . str_replace('\*', '.*', preg_quote($filenamePattern, '/')) . '$/';

                    $contents = $fileRepository->setServer($this->server)->getDirectory($directory);

                    foreach ($contents as $content) {
                        if (preg_match($regexPattern, $content['name'])) {
                            $fileRepository->setServer($this->server)->deleteFiles($directory, [$content['name']]);
                        }
                    }
                } else {
                    $fileRepository->setServer($this->server)->deleteFiles($directory, [$filenamePattern]);
                }
            }
        }

        if (Arr::get($this->data, 'blueprints')) {
            $fileRepository->setServer($this->server)->deleteFiles(
                '/server/rust',
                ['player.blueprints.5.db', 'player.blueprints.5.db-journal'],
            );
        }

        $powerRepository->setServer($this->server)->send('start');
    }
}