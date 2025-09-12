<?php

namespace Pterodactyl\Transformers\Api\Client;

use Pterodactyl\Models\Wipe;
use Pterodactyl\Models\WipeCommand;
use League\Fractal\Resource\Collection;

class WipeTransformer extends BaseClientTransformer
{
    /**
     * List of resources to automatically include
     *
     * @var array
     */
    protected array $defaultIncludes = [
        'commands'
    ];

    /**
     * Return the resource name for the JSONAPI output.
     */
    public function getResourceName(): string
    {
        return Wipe::RESOURCE_NAME;
    }

    /**
     * A Fractal transformer.
     *
     * @return array
     */
    public function transform(Wipe $model)
    {
        return $model->toArray();
    }

    /**
     * Returns the server commands associated with this wipe.
     *
     * @throws \Pterodactyl\Exceptions\Transformer\InvalidTransformerLevelException
     */
    public function includeCommands(Wipe $model): Collection
    {
        return $this->collection(
            $model->commands,
            $this->makeTransformer(WipeCommandTransformer::class),
            WipeCommand::RESOURCE_NAME
        );
    }
}
