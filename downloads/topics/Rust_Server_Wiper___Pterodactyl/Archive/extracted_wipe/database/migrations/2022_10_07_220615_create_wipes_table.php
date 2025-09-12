<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class CreateWipesTable extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::create('wipes', function (Blueprint $table) {
            $table->id();
            $table->char('uuid', 36)->unique();
            $table->integer('server_id')->unsigned();
            $table->string('name');
            $table->string('description');
            $table->integer('size')->nullable();
            $table->integer('seed')->nullable();
            $table->boolean('random_seed');
            $table->string('level')->nullable();
            $table->text('files');
            $table->boolean('blueprints');
            $table->datetime('time');
            $table->datetime('ran_at')->nullable();
            $table->timestamps();
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::dropIfExists('wipes');
    }
}
