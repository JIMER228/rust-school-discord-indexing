<?php

namespace Pterodactyl\Http\Requests\Api\Client\Servers\Wipe;

use Pterodactyl\Models\Wipe;
use Pterodactyl\Models\Permission;
use Pterodactyl\Http\Requests\Api\Client\ClientApiRequest;

class WipeRequest extends ClientApiRequest
{
    /**
     * Determine if the API user has permission to perform this action.
     */
    public function permission(): string
    {
        return Permission::ACTION_WIPE_MANAGE;
    }

    /**
     * Get the validation rules that apply to the request.
     *
     * @return array
     */
    public function rules(): array
    {
        return [
            'name' => Wipe::getRules()['name'],
            'description' => Wipe::getRules()['description'],
            'size' => Wipe::getRules()['size'],
            'seed' => Wipe::getRules()['seed'],
            'random_seed' => Wipe::getRules()['random_seed'],
            'random_level' => Wipe::getRules()['random_level'],
            'level' => Wipe::getRules()['level'],
            'files' => Wipe::getRules()['files'],
            'commands' => 'sometimes|array',
            'command_times' => 'sometimes|array',
            'blueprints' => Wipe::getRules()['blueprints'],
            'schedule' => 'boolean',
            'time' => 'nullable|date',
            'force' => Wipe::getRules()['force'],
            'repeat' => Wipe::getRules()['repeat'],
        ];
    }
}
