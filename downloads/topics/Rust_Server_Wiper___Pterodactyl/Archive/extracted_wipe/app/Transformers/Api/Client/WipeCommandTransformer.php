<?php

namespace Pterodactyl\Transformers\Api\Client;

use Pterodactyl\Models\WipeCommand;

class WipeCommandTransformer extends BaseClientTransformer
{
    /**
     * Return the resource name for the JSONAPI output.
     */
    public function getResourceName(): string
    {
        return WipeCommand::RESOURCE_NAME;
    }

    /**
     * A Fractal transformer.
     *
     * @return array
     */
    public function transform(WipeCommand $model)
    {
        return $model->toArray();
    }
}
