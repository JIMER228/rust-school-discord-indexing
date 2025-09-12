<?php

namespace Pterodactyl\Http\Requests\Api\Client\Servers\Wipe;

use Pterodactyl\Models\Permission;
use Pterodactyl\Http\Requests\Api\Client\ClientApiRequest;

class GetWipesRequest extends ClientApiRequest
{
    /**
     * Determine if the API user has permission to perform this action.
     */
    public function permission(): string
    {
        return Permission::ACTION_WIPE_MANAGE;
    }
}
