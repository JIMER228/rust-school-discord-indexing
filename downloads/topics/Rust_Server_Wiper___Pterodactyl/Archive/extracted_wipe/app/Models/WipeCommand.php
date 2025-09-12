<?php

namespace Pterodactyl\Models;

class WipeCommand extends Model
{
    public const RESOURCE_NAME = 'wipe_command';

    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'wipe_commands';

    /**
     * The attributes that should be mutated to dates.
     *
     * @var array
     */
    protected $dates = [self::CREATED_AT, self::UPDATED_AT];

    /**
     * Fields that are not mass assignable.
     *
     * @var array
     */
    protected $guarded = ['id', self::CREATED_AT, self::UPDATED_AT];

    /**
     * @var array
     */
    public static array $validationRules = [
        'wipe_id' => 'bail|required|numeric|exists:wipes,id',
        'command' => 'required|string',
        'time' => 'required|numeric|min:1|max:60',
    ];
}
